// define to do some extra checking as we save the data out
// #define EXTRA_DEBUG_CHECKS

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Diz.Core.model;
using ExtendedXmlSerializer.ContentModel;
using ExtendedXmlSerializer.ContentModel.Format;

// everything else in the savefiles is straight up normal XML, but,
// the amount of bytes for the ROM metadata can be huge.
// by using a custom serializer for this one section, we can cut a 4MB rom savefile down from ~45MB to ~1.5MB
//
// This uses some hacky compression methods designed to preserve:
// 1) newlines
// 2) slight human readability for merging, so projects can be collaborated on in source control
// 3) using pattern substitution tables that won't change from PC to PC.
//
// The easiest thing would be use zlib (and we can use it with the output of the entire XML if we want)
// but, for projects with multiple collaborators using diztinGUIsh, mergability in text/git/editors/etc
// is a must.  We aim for a tradeoff between decent compression and some small semblance of human readability.
//
// It's not.. super-pretty code, but it compresses well.
namespace Diz.Core.serialization.xml_serializer;

[TypeConverter(typeof(ExpandableObjectConverter))]
public class RomBytesOutputFormatSettings
{
    // note: some of these will only do anything the NEXT time a file is saved to disk.
    // this struct will also be serialized with the project.

    // These settings control some (ad-hoc / hacky) compression for output text.
    // The goal is *purely* to reduce the size of the final XML file, but do so in a way that is still [somewhat] mergeable
    // for team collaboration using tools like 'git merge' etc.
    //
    // These settings ONLY affect data that's being serialized (Save project) and whether we attempt to compress it a bit more.
    // For data that's being deserialized (during Load Project), we need to always be able to deal with these encodings.
    //
    // For the maximually mergeable version (but, at the cost of slightly larger filesize), leave both OFF.
        
        
    /// <summary>
    /// Run-length compression for the "RomBytes" special data section in the output file.
    /// Find output lines that are identical, and use run-length encoding to mark how many times a line should be repeated.
    /// Diz projects tend to be CRAMMED FULL of duplicate lines, especially for huge unbroken sections of content/graphics/music/etc.
    /// Enabling this is a huge win for filesize, but, at the expensive of merge-friendliness
    /// It's a pretty good choice to leave this ON by default.
    ///
    /// This is only used when saving, not loading.
    /// </summary>
    /// <value>
    /// <c>true</c> if the group block should be compressed on save; otherwise, <c>false</c>.
    /// </value>
    [DisplayName("Project file: Enable Run-Length Encoding compression")]
    [Description(
        "ADVANCED: Defaults to ON. If disabled, don't apply run-length encoding to your .diz/.dizraw save file. " +
        "Disable this if you want to keep all lines in your save file 1:1 with bytes from the original ROM." +
        "The only reason you care about this is if you are working on a team and want to merge .dizraw files together in git more easily." +
        "This option will take the save file and increase it to about 10x the size on average (still not a big deal though)"
    )]
    public bool CompressGroupBlock { get; set; } = true;
        
    /// <summary>
    /// CompressUsingTable1: Take the output lines, and run some substitutions for the most common patterns we see in Diz files
    /// (as of a few analyzed in 2021). This is like an incredibly crappy gzip-style encoding, but, optimized to still output readable text that 
    /// preserves line breaks (so it can still be merged as a text file with external tools).
    /// 
    /// This won't change the # of output lines (just their content), so it's pretty merge-friendly.
    /// Humans won't be able to read the lines well though, it'll just look like "base64"-ish gibberish.
    /// 
    /// Still, a pretty nice win and solid tradeoff of filesize and human text merging friendliness.
    ///
    /// This is only used when saving, not loading.
    /// </summary>
    /// <value>
    /// <c>true</c> if the text should be substituted for common patterns on save
    /// </value>
    [DisplayName("Project file: Enable per-line substitution")]
    [Description(
        "ADVANCED: Defaults to ON. Disable to make the .dizraw file a bit more readable, at the expense of file size. No real reason to turn this off."
    )]
    public bool CompressUsingTable1 { get; set; } = true;

    /// <summary>
    /// Optional. if not zero, then every N output lines in the XML file's RomBytes section we'll output a comment
    /// with the ROM offset.  this is 100% optional, it's purely for humans and merge tools to be able to deal with
    /// less search area when doing tricky merges.
    /// Comments are ignored on load.
    /// This is only used when saving, not loading.
    /// Smaller numbers here make easier merges, but, increase the filesize.
    /// 0x4000 is a nice tradeoff between negligible file increase and much better merge friendliness 
    /// </summary>
    [DisplayName("Project file: Break up lines in .diz file with offset comments")]
    [Description(
        "If non-zero, in the .diz/.dizraw project file, add comments every N bytes in the .dizraw file. " +
        "Lower this number to increase mergability or diff viewing when usig git/other text merge tools."
    )]
    public int InsertCommentIntoOutputLinesEveryNBytes { get; set; } = 0x800;
        
    public override string ToString() => "";
}

    
sealed class RomBytesSerializer : ISerializer<RomBytes>
{
    // let the outer XML class do the heavy lifting on versioning.
    // but, let's add one here just because this specific class is complex.
    // CHANGE THIS if you make breaking changes to this file (please try not to).
    // if you DO, make sure to add a migration, or, mark that one isn't needed.
    // history of data format changes:
    //  version     description
    //  -------     ----------------
    //  200         initial data format
    //  201         add ability to add comments
    private const int MaxSupportedDataFormatVersion = 201;
        
    // this is the oldest data format we'll attempt to read. If it's older, it's not supported.
    private const int OldestAllowedDataFormatVersion = 200;

    public static RomBytesSerializer Default { get; } = new();

    public RomBytesOutputFormatSettings FormatSettings = new();
        
    public int NumTasksToUse { get; init; } = 5; // seems like the sweet spot

    public RomBytes Get(IFormatReader parameter)
    {
        var allLines = ReadMainDataRaw(parameter.Content());
        var romBytes = DecodeAllBytes(allLines);
        return FinishRead(romBytes);
    }

    private RomByte[] DecodeAllBytes(IReadOnlyList<string> allLines)
    {
        if (NumTasksToUse == 1)
            return DecodeRomBytes(allLines, 0, allLines.Count);

        var tasks = new List<Task<RomByte[]>>(NumTasksToUse);

        var nextIndex = 0;
        var workListCount = allLines.Count / NumTasksToUse;

        for (var t = 0; t < NumTasksToUse; ++t)
        {
            if (t == NumTasksToUse - 1)
                workListCount = allLines.Count - nextIndex;

            tasks.Add(CreateDecodeRomBytesTask(allLines, nextIndex, workListCount));

            nextIndex += workListCount;
        }

        var continuation = Task.WhenAll(tasks);
        continuation.Wait();
        return continuation.Result.SelectMany(i => i).ToArray();
    }

    private static Task<RomByte[]> CreateDecodeRomBytesTask(IReadOnlyList<string> allLines, int nextIndex, int workListCount)
    {
        // ReSharper disable once AccessToStaticMemberViaDerivedType
        return Task<RomByte[]>.Run(() => DecodeRomBytes(allLines, nextIndex, workListCount));
    }

    private static RomByte[] DecodeRomBytes(IReadOnlyList<string> allLines, int startIndex, int count)
    {
        // perf: allocate all at once, don't use List.Add() one at a time
        var romBytes = new RomByte[count];
        var romByteEncoding = new RomByteEncoding();
        var i = 0;

        try
        {
            while (i < count)
            {
                var line = allLines[startIndex + i];
                romBytes[i] = romByteEncoding.DecodeRomByte(line);
                ++i;
            }
        }
        catch (Exception ex)
        {
            ex.Data.Add("ParseLineNum", "Near line# " + (startIndex + i));
            throw;
        }

        return romBytes;
    }

    private static RomBytes FinishRead(RomByte[] romBytes)
    {
        var romBytesOut = new RomBytes();
        romBytesOut.SetFrom(romBytes);
        return romBytesOut;
    }

    private static List<string> ReadMainDataRaw(string allLines)
    {
        // first line is the header
        var (lines, options) = ReadHeader(allLines);
        CheckForCompatibleVersion(options);
            
        var encodedWithCompressTable1 = options.Exists(s => s == "compress_table_1");
        var encodedWithCompressGroupBlocks = options.Exists(s => s == "compress_groupblocks");
            
        // everything after this is the actual data in the file.
        // no matter what, it should be newline-delimited.
        // IMPORTANT: always apply these operations in the reverse order of the SAVE function.
        if (encodedWithCompressTable1)
            SubstitutionCompression.DecodeCompression_Table1(ref lines);
            
        if (encodedWithCompressGroupBlocks)
            RepeaterCompression.Decompress(ref lines);
            
        return lines
            .Select(line => line.Contains(';') ? line[..line.IndexOf(';')].TrimEnd() : line) // remove any comments
            .Where(line => !string.IsNullOrWhiteSpace(line)) // remove any blank lines (including newly blanked lines because they used to have a comment in them)
            .ToList();
    }

    private static (List<string> lines, List<string> options) ReadHeader(string allLines)
    {
        // clean... this...
        var lines = allLines.Split(new char[] {'\n'}, 3).ToList();
        var options = lines[1].Split(new char[] {','}).ToList();
        lines = lines[2].Split(new char[] {'\n'}).ToList();
        if (lines[lines.Count - 1] == "")
            lines.RemoveAt(lines.Count - 1);
        return (lines, options);
    }

    private static void CheckForCompatibleVersion(IEnumerable<string> options)
    {
        try
        {
            var versionNum = ParseVersionNumFromOptions(options);

            // if we hit this, we're trying to open a file saved with a newer version of Diz.
            // we should bail. Try to avoid things that break this.
            if (versionNum > MaxSupportedDataFormatVersion)
                throw new InvalidDataException(
                    $"Newer ROMBytes section format detected: version={versionNum}. This version of distinguish only supports data table formats up to {MaxSupportedDataFormatVersion}.");
                
            // if we're at the correct data version, all good.
            if (versionNum == MaxSupportedDataFormatVersion) 
                return;
                
            // otherwise, we are opening a file saved with an older data format and may need to convert input data to our newer format.
            // 
            // dev notes: when the data version changes, please write migrations here that convert old data to new data
            // (or, leave a note that nothing breaking changed and no migration is actually needed)
                
            // too old, we're dead
            if (versionNum < OldestAllowedDataFormatVersion)
                throw new InvalidDataException(
                    $"Older ROMBytes section format detected: version={versionNum},"
                    + " but, we don't know how to convert it to the newer format"
                    + " (this is probably not legit and is likely a bug, please report it)." +
                    $" This version of distinguish only supports ROMBytes section formats up to version={MaxSupportedDataFormatVersion}."
                );
                
            // we're at an older version, but, we can run the migrations below to upgrade to newer versions 
                    
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (versionNum == 200)
            {
                // v200 was the original data format.
                // to upgrade v200 to v201, we do: nothing :)
                // v201 simply adds support for ignoring comments on lines. v200 files will never have comments.
                // older versions of diz will choke on comments. our version will simply ignore comments
                versionNum = 201; // nothing to migrate, we're done, mark us as being at version 201.
            }
                    
            // at the end of our migrations, our data must have been changed (if necessary) and the version# must match the latest.
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (versionNum != MaxSupportedDataFormatVersion)
            {
                throw new InvalidDataException(
                    $"BUG: (please report)" 
                    + " ROMBytes data migration from older version failed: we did update to version={versionNum},"
                    + $" but it should have reached CurrentDataFormatVersion={MaxSupportedDataFormatVersion}."
                );
            }
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Data table loader: Version error: {ex.Message}");
        }
    }

    private static int ParseVersionNumFromOptions(IEnumerable<string> options)
    {
        var versionOption = options.SingleOrDefault(s => s.Contains("version:"));

        if (versionOption == null)
        {
            throw new InvalidDataException(
                $"Exactly 1 'version' tag must be in options, unable to continue");
        }

        var split = versionOption.Split(':');
        Debug.Assert(split.Length == 2);
        if (!int.TryParse(split[1], out var versionNum))
            throw new InvalidDataException(
                $"Couldn't parse version # from RomBytes version tag");
        return versionNum;
    }

    public void Write(IFormatWriter writer, RomBytes instance)
    {
        var options = new List<string>
        {
            $"version:{MaxSupportedDataFormatVersion}",
        };

        var lines = new List<string>(capacity: instance.Count);
            
        // generate all text lines.
        for (var romOffset = 0; romOffset < instance.Count; romOffset++)
        {
            // the important stuff: generate the real data, this is the entire point:
            var rb = instance[romOffset];
            var encodedTxt = RomByteEncoding.EncodeByte(rb);
            lines.Add(encodedTxt);
                
            // completely optional: output a comment every N bytes. this is purely to make it easier for humans to read and 
            // use merge tools like git to figure out where they might be in the file. otherwise, it's basically impossible to know where you are in the file
            // if you're just looking at it in in a text editor.
            // note: we want to have these comment breaks FIRST so that the RLE compression in CompressGroupBlock stops at these boundaries.
            // i.e. no matter what wacky stuff the RLE compression does, the comments will remain at constant in any output file.
            // hopefully these comments serve as good 'anchors' in git merge algorithms.
            if (FormatSettings.InsertCommentIntoOutputLinesEveryNBytes > 0 && romOffset > 0 && romOffset % FormatSettings.InsertCommentIntoOutputLinesEveryNBytes == 0)
            {
                // at this lower level we do have all the rom bytes but we don't have bank/mapping info.
                // so, try to keep InsertCommentIntoOutputLinesEveryNBytes a multiple of something that lines up on every bank length on the SNES.
                lines.Add($";pos={romOffset:X6}");
            }

            // debug check, optional:
#if EXTRA_DEBUG_CHECKS
                var decoded = romByteEncoding.DecodeRomByte(encodedTxt);
                Debug.Assert(decoded.EqualsButNoRomByte(rb));
#endif
        }

        // all our output data is finished and we could write it to the file right now and it would be valid. 
        // HOWEVER, we can run some postprocessors for transforming the data (via some light compression, adding comments, etc)
        // for the sake of merge-friendliness or other uses.
        PostProcessOutputData(options, ref lines);

        writer.Content($"\n{string.Join(",", options)}\n");

        foreach (var line in lines)
        {
            writer.Content(line);
            writer.Content("\n");
        }
    }

    private void PostProcessOutputData(ICollection<string> options, ref List<string> lines)
    {
        if (FormatSettings.CompressGroupBlock)
        {
            options.Add("compress_groupblocks");
            RepeaterCompression.Compress(ref lines);
        }

        if (FormatSettings.CompressUsingTable1)
        {
            options.Add("compress_table_1");
            SubstitutionCompression.EncodeCompression_Table1(ref lines);
        }
    }
}