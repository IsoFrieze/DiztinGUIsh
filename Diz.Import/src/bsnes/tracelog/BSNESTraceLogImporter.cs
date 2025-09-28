using System.Collections.Concurrent;
using Diz.Core.Interfaces;
using Diz.Cpu._65816;

namespace Diz.Import.bsnes.tracelog;

public partial class BsnesTraceLogImporter
{
    private readonly ISnesData? snesData = null;

    // note: we could add directly to snesData.Data.Comments BUT locking is too slow on a normal dict,
    // and updating the rest of the app for ConcurrentDictionary is a bit painful at the moment.
    // so, we'll buffer any generated comments here in this dict, and at the end we'll copy everything here into 
    // its final home in snesData.Data.Comments
    private ConcurrentDictionary<int, string> tracelogCommentsGenerated = new();

    // these are cached mostly to save us from having to lock Data.
    // do not allow these to change over the life of the importer
    private readonly int romSizeCached;
    private readonly RomMapMode romMapModeCached;

    public BsnesTraceLogImporter(ISnesData? snesData)
    {
        this.snesData = snesData;
        romSizeCached = this.snesData?.GetRomSize() ?? 0;
        romMapModeCached = this.snesData?.RomMapMode ?? default;

        modificationDataPool = new ObjPool<ModificationData>();
        InitStats();
    }

    // Mark collected trace data for a RomByte (which should be an opcode) AND any of the operands that follow us.
    // note: snes address provided SHOULD always be an opcode (not operand or data).
    // it may not always be a valid rom address though (it could be running from RAM/etc)
    private void ConsumeAndFreeTraceData(ref ModificationData? modData, int instructionByteLen = -1)
    {
        // WARNING: extremely performance-intensive function. be really careful when changing stuff.
        // please profile any changes made here. the slightest thing can break the concurrency and render
        // the entire tracelog capture system useless.  use DotTrace or similar tools for profiling.
        
        if (modData == null)
            return;
        
        // ----------------
        // part 1: comments (optional but useful side feature)
        // ----------------
        // NOTE: you can't count on the stuff in snesData to be updated yet.
        // when we get here, this function is called on an opcode [instruction] BSNES has given us,
        // and below will handle marking it as opcodes vs operands.
        UpdateTracelogComments(modData.SnesAddress, in modData.CaptureSettings);
        
        // ----------------
        // part 2: (the main important thing)
        // mark flags in Diz project as opcode vs operand (and record MX flags, D, BD, etc)
        // ----------------
        var currentOffset = 0;
        var numBytesAnalyzed = 0;

        // we start at position = 0 as an opcode.
        // we then traverse the length of this opcode 
        while (true)
        {
            // prep
            numBytesAnalyzed++;
            
            // rejects any non-ROM based addresses (like stuff running in RAM)
            modData.Pc = ConvertSnesToPc(modData.SnesAddress);
            if (!IsOkToSetThisRomByte(modData.Pc, instructionByteLen, currentOffset))
                break;

            // finally, modify Diz project: update MX flags and opcode/operand status
            modData.FlagType = GetFlagForInstructionPosition(currentOffset);
            if (!modData.CaptureSettings.CaptureLabelsOnly)
            {
                var romByte = snesData!.Data.RomBytes[modData.Pc];
                modData.ApplyModificationIfNeeded(romByte);
                UpdateStats(modData);
            }

            // prep for processing followup bytes (operands)
            modData.SnesAddress = GetNextSNESAddress(modData.SnesAddress);
            currentOffset++;
        }

        FreeModificationData(ref modData); // sets this to Null after called

        currentStats.NumRomBytesAnalyzed += numBytesAnalyzed;
    }
    
    // assumption: SNES address is an OPCODE that BSNES has identified (it may not be marked in our project yet though).
    // do NOT count on any data (Flags i.e. MX, and whether this is an opcode vs operand vs data) yet being set for this
    // opcode in the Diz project. that may happen AFTER this function is called.
    //
    // WARNING: Snes address can be ANYTHING including instructions executing in RAM. it may not map to a ROM address.
    private void UpdateTracelogComments(int snesAddress, in BsnesTraceLogCaptureController.TraceLogCaptureSettings traceLogCaptureSettings)
    {
        string? commentText = null;
        if (traceLogCaptureSettings is { AddTracelogLabel: true, CommentTextToAdd.Length: > 0 })
        {
            commentText = "TLC:" + traceLogCaptureSettings.CommentTextToAdd;
        } 
        else if (traceLogCaptureSettings.RemoveTracelogLabels)
        {
            commentText = "XX*-remove-*XX";
        }
        
        // if adding OR removing, we need to add something to the comment list.
        // later, we'll take the entries from this temp list and put them in the Diz project.
        // we could do it here directly, but, it's too slow.
        if (commentText != null)
            tracelogCommentsGenerated.AddOrUpdate(snesAddress, commentText, (_, _) => commentText);
    }
    // private void UpdateTracelogComments(int snesAddress, in BsnesTraceLogCapture.TraceLogCaptureSettings traceLogCaptureSettings)
    // {
    //     tracelogCommentsGenerated.TryGetValue(snesAddress, out var comment);
    //
    //     // 2 things here: 
    //     //   1. if requested, remove any TLC ("Trace Log Capture") comments 
    //     var shouldRemoveExisting = traceLogCaptureSettings.RemoveTracelogLabels && comment != null && comment.StartsWith("TLC:");
    //
    //     //   2. if requested, add or replace any TLC comments
    //     var shouldAdd = traceLogCaptureSettings is { AddTracelogLabel: true, CommentTextToAdd.Length: > 0 };
    //
    //     // are we allowed to replace an existing comment?
    //     if (comment != null && shouldAdd)
    //     {
    //         if (!comment.StartsWith("TLC:"))
    //             shouldAdd = false; // no. can't overwrite a non-tracelog comment
    //         else
    //             shouldRemoveExisting = true; // fine, but we'll have to remove the existing first
    //     }
    //
    //     switch (shouldRemoveExisting)
    //     {
    //         // do we actually need to do anything?
    //         case false when !shouldAdd:
    //             return;
    //         case true:
    //             tracelogCommentsGenerated.TryRemove(snesAddress, out _);
    //             break;
    //     }
    //
    //     if (!shouldAdd) 
    //         return;
    //     
    //     var newComment = "TLC:" + traceLogCaptureSettings.CommentTextToAdd;
    //     tracelogCommentsGenerated.AddOrUpdate(snesAddress, newComment, (_, _) => newComment);
    // }

    private bool IsOkToSetThisRomByte(int pc, int instructionByteLen, int opIndex)
    {
        if (pc < 0 || pc >= romSizeCached)
            return false;

        ValidateInstructionByteLen(instructionByteLen);
        if (opIndex is < 0 or > 4) // using *4* here, not the real SNES max possible of 3.  calling code will deal with invalid '4' case
        {
            throw new InvalidDataException($"Invalid opcode index {opIndex}. Must be between 0 and 4");
        }

        if (instructionByteLen != -1)
        {
            // normal case: just make sure we're in range if BSNES has told us the amount of bytes we need to process
            return opIndex < instructionByteLen;
        }
        // OTHERWISE: (weirder case) we haven't been told how many bytes to process, so have to do some fuzzy guessing now. play it safe.

        // easy case: this is the first byte, this will be the Opcode, so clear that for takeoff.
        if (opIndex == 0)
            return true;

        // otherwise, this is NOT the first byte (Opcode), and we don't have information about how many bytes
        // past us are Operands. Could be none, could be up to 3.
        //
        // We're trying to mark as many bytes as operands with the flags from the tracelog.
        // We can't safely know though, so, unless they've ALREADY been marked as Operands, let's
        // just play it safe and stop at the first thing that's NOT an Operand.
        //
        // Calling code should ideally not let us get to here, and instead supply us with a valid instructionByteLen
        return GetFlag(pc) == FlagType.Operand;
    }

    private FlagType GetFlag(int pc)
    {
        snesData!.Data.RomBytes[pc].Lock.EnterReadLock();
        try
        {
            return snesData.GetFlag(pc);
        }
        finally
        {
            snesData.Data.RomBytes[pc].Lock.ExitReadLock();
        }
    }

    private static void ValidateInstructionByteLen(int instructionByteLen)
    {
        if (instructionByteLen != -1 && instructionByteLen is < 1 or > 4)
        {
            throw new InvalidDataException(
                $"Invalid opcode+operand byte length {instructionByteLen}. Must be -1, or between 1 and 4");
        }
    }

    public void CopyTempGeneratedCommentsIntoMainSnesData()
    {
        // final step: copy temp generated keys back into snesData.
        // this is done using a temporary dictionary only for performance reasons (locking is too slow to keep up with the SNES without
        // using ConcurrentDictionary)

        if (snesData == null)
            return;
        
        // we will allow overwriting existing data in this.snesData.Data.Comments ONLY IF the destination existing kvp values 
        // start with the string "TLC". otherwise, we won't allow overwriting.
        foreach (var tlcGenerateComment in tracelogCommentsGenerated)
        {
            var commentAlreadyExists = snesData.Data.Comments.TryGetValue(tlcGenerateComment.Key, out var val);
            
            // we're only allowed to modify tracelog comments
            if (commentAlreadyExists && val != null && !val.StartsWith("TLC:"))
                continue;
            
            // if we get here either there's NO comment already existing, or, it's a tracelog comment and we can overwrite it
            
            // REMOVE: if this SNES address is marked for removal... 
            if (tlcGenerateComment.Value == "XX*-remove-*XX")
            {
                if (snesData.Data.Comments.ContainsKey(tlcGenerateComment.Key))
                    snesData.Data.Comments.Remove(tlcGenerateComment.Key);
                
                continue;
            }
            
            // ADD: add this tracelog value and overwrite anything there
            // (This adds the key if it doesn't exist, updates if it already does exist)
            snesData.Data.Comments[tlcGenerateComment.Key] = tlcGenerateComment.Value;
        }
    }
}