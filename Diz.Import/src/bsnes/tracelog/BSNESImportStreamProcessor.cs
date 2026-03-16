// #define PROFILING

using System.Collections.Concurrent;
using System.Diagnostics;
using Diz.Core.util;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace Diz.Import.bsnes.tracelog;

public abstract class PoolItem
{
    public bool IsFree = true;
}

public class ObjPool<T> where T : PoolItem, new()
{
    private readonly ConcurrentStack<T> freeObjects;

    public ObjPool()
    {
        freeObjects = new ConcurrentStack<T>();
    }

    public T Get()
    {
        var item = Alloc();
        Debug.Assert(item.IsFree);
        item.IsFree = false;
        return item;
    }

    private T Alloc()
    {
        return freeObjects.TryPop(out var item) ? item : new T();
    }

    public void Return(ref T? item)
    {
        if (item == null)
            return;
            
        Debug.Assert(!item.IsFree);
        item.IsFree = true;
        DeAlloc(item);

        item = null;
    }

    private void DeAlloc(T item)
    {
        freeObjects.Push(item);
    }
}

public class BsnesImportStreamProcessor
{
    public class WorkItemDecompressSnesTraces : PoolItem
    {
        // input: a compressed buffer sent to us by BSNES with hundreds of tracelog instructions
        public const int HeaderSize = 9;
        public byte[]? Header;                  // 1 byte ID, 4 bytes CompressedBufferSize (which is gzip'd) to follow. 4 bytes: length of uncompressed data 
        public byte[]? CompressedBuffer;
        public int CompressedSize;
        
        // output: a buffer where the compressed buffer input will be extracted to.
        public byte[]? UncompressedBuffer;
        public int UncompressedSize;
        public bool WasDecompressed;

        public byte[]? ScratchBuffer;
        
        public readonly List<WorkItemSnesTrace?>? ListHeads = new();

        // copy of the settings as they existed at the moment of capture
        public BsnesTraceLogCaptureController.TraceLogCaptureSettings CaptureSettings { get; set;  } = new();
    }

    // represents both a SNES trace item.
    // performance: this is ALSO stored as a linked list that can be traversed by a worker thread
    // to act as a work queue.
    // performance note: we need to process an extremely high volume of these. at this level,
    // every memory allocation and CPU operation counts.
    public class WorkItemSnesTrace : PoolItem
    {
        public byte[]? Buffer;
        public bool AbridgedFormat;
        
        // copy of the settings as they existed at the moment of original capture
        public BsnesTraceLogCaptureController.TraceLogCaptureSettings CaptureSettings { get; } = new();
        
        // linked list: reference to the next SNES trace that we should process
        public WorkItemSnesTrace? Next;
    }

    private ObjPool<WorkItemDecompressSnesTraces>? poolCompressedWorkItems;
    private ObjPool<WorkItemSnesTrace>? poolWorkItems;

    public CancellationTokenSource CancelToken { get; set; } = new();

    public BsnesImportStreamProcessor(bool poolAllocations = true)
    {
        if (!poolAllocations)
            return;

        poolCompressedWorkItems = new ObjPool<WorkItemDecompressSnesTraces>();
        poolWorkItems = new ObjPool<WorkItemSnesTrace>();
    }

    public void Shutdown()
    {
        poolWorkItems = null;
        poolCompressedWorkItems = null;

        // reset for next time around
        CancelToken = new CancellationTokenSource();
    }

    public IEnumerable<WorkItemDecompressSnesTraces> GetCompressedWorkItems(Stream? stream)
    {
        while (!CancelToken.IsCancellationRequested)
        {
            // perf: huge volume of data coming in.
            // need to get the data read from the server as fast as possible.
            // do minimal processing here then dump it over to the other thread for real processing.
            var compressedWorkItem = ReadPacketFromStream(stream);
            if (compressedWorkItem == null)
                break;
            
            // prob right here, add in the current settings so we capture them.
            // TODO:

            yield return compressedWorkItem;
        }
    }

    private WorkItemDecompressSnesTraces? ReadPacketFromStream(Stream? stream)
    {
        if (stream is null) throw new ArgumentNullException(paramName: nameof(stream));

        #if PROFILING
        var mainSpan = Markers.EnterSpan("BSNES socket read");
        #endif

        var item = AllocateCompressedWorkItem();
        Debug.Assert(item.Header != null);

        try
        {
            Util.ReadNext(stream, item.Header, WorkItemDecompressSnesTraces.HeaderSize);
        }
        catch (EndOfStreamException)
        {
            FreeCompressedWorkItem(ref item);
            return null;
        }

        #if PROFILING
        Markers.WriteFlag("initial read");
        #endif

        if (item.Header.Length != WorkItemDecompressSnesTraces.HeaderSize)
            throw new InvalidDataException($"invalid header length for compressed data chunk");

        if (item.Header[0] != 'Z')
            throw new InvalidDataException($"expected header byte of 'Z', got {item.Header[0]} instead.");

        item.UncompressedSize = ByteUtil.ConvertByteArrayToInt32(item.Header, 1);
        item.CompressedSize = ByteUtil.ConvertByteArrayToInt32(item.Header, 5);

        // allocation pool.  if we need to allocate for compressed data, let's go slightly higher so that
        // we have a chance of re-using this buffer without needing to re-allocate.
        const float
            unscientificGuessAtExtraWeShouldAdd = 1.3f; // total guess. adjust higher if you do too many allocations
        if (item.CompressedBuffer == null || item.CompressedBuffer.Length < item.CompressedSize)
            item.CompressedBuffer = new byte[(int) (item.CompressedSize * unscientificGuessAtExtraWeShouldAdd)];

        #if PROFILING
        Markers.WriteFlag("big read start");
        #endif
        
        int bytesRead;
        try
        {
            bytesRead = Util.ReadNext(stream, item.CompressedBuffer, item.CompressedSize);
        }
        catch (EndOfStreamException)
        {
            FreeCompressedWorkItem(ref item);
            return null;
        }

        #if PROFILING
        Markers.WriteFlag("big read done");
        #endif

        if (bytesRead != item.CompressedSize)
        {
            throw new InvalidDataException(
                $"compressed data: expected {item.CompressedSize} bytes, only got {bytesRead}");
        }

        #if PROFILING
        mainSpan.Leave();
        #endif

        return item;
    }

    private WorkItemDecompressSnesTraces AllocateCompressedWorkItem()
    {
        var item = AllocCompressedWorkItem();

        item.CompressedSize = item.UncompressedSize = 0;
        item.WasDecompressed = false;

        // did we already allocate this before, and is it the same size?
        if (item.Header is { Length: WorkItemDecompressSnesTraces.HeaderSize })
            return item; // we did! optimized path OK

        // un-optimized path: allocate it so we can use it (happens on first use too)
        item.Header = new byte[WorkItemDecompressSnesTraces.HeaderSize];
        return item;
    }

    private WorkItemDecompressSnesTraces AllocCompressedWorkItem()
    {
        return poolCompressedWorkItems == null ? new WorkItemDecompressSnesTraces() : poolCompressedWorkItems.Get();
    }

    private WorkItemSnesTrace AllocWorkItem()
    {
        return poolWorkItems == null ? new WorkItemSnesTrace() : poolWorkItems.Get();
    }

    public void FreeCompressedWorkItem(ref WorkItemDecompressSnesTraces? compressedItem)
    {
        // performance: remember: we don't want to kill the big buffers.
        // main point of this pool is to hopefully re-use them later.

        if (compressedItem == null)
            return;

        // keep the capacity, but kill the contents.
        // also, go a little overkill and kill the references to WorkItem list heads inside the List.
        if (compressedItem.ListHeads != null)
        {
            for (var i = 0; i < compressedItem.ListHeads.Count; ++i)
            {
                compressedItem.ListHeads[0] = null;
            }

            compressedItem.ListHeads.Clear();
        }

        poolCompressedWorkItems?.Return(ref compressedItem);
    }

    public void FreeWorkItem(ref WorkItemSnesTrace? workItem)
    {
        if (workItem == null)
            return;

        // don't kill the big buffers. main point of this pool is to hopefully re-use them later.

        workItem.Next = null;

        poolWorkItems?.Return(ref workItem);
    }

    private WorkItemSnesTrace AllocateWorkItem(byte workItemLen)
    {
        var workItem = AllocWorkItem();

        // turn this on if you ever think you have a memory allocation issue
        #if DEBUG_ALLOC_CHECKING
        if (workItem.isFree || workItem.Next != null)
        {
            Debugger.Break();
        }
        #endif

        workItem.AbridgedFormat = false;

        // almost every item re-used from the pool should have the correct buffer size already.
        // this size is usually fixed, so ok to check for exact len.
        if (workItem.Buffer != null && workItem.Buffer.Length == workItemLen)
            return workItem;

        // this is what we're trying to avoid re-allocating
        // (but we will have to allocate on the first use)
        workItem.Buffer = new byte[workItemLen];

        return workItem;
    }

    private static void AllocateUncompressedBuffer(WorkItemDecompressSnesTraces workItemDecompressSnesTraces)
    {
        Debug.Assert(workItemDecompressSnesTraces.UncompressedSize != 0);
        // remember, our goal is to reduce the # of allocations done for compressedWorkItem.UncompressedBuffer
        // CompressedWorkItems are cached as a pool, and if our buffer size is large enough, we can
        // avoid a re-allocation.

        if (workItemDecompressSnesTraces.UncompressedBuffer != null &&
            workItemDecompressSnesTraces.UncompressedBuffer.Length >= workItemDecompressSnesTraces.UncompressedSize)
            return;

        // go a little more than we need to avoid
        // re-use needing to re-allocate.
        // works because our uncompressedsize shouldn't change much
        const float unscientificMultiplierGuess = 1.2f;

        var size = (int) (workItemDecompressSnesTraces.UncompressedSize * unscientificMultiplierGuess);
        workItemDecompressSnesTraces.UncompressedBuffer = new byte[size];
    }

    // take CompressedBuffer and un-gzip it into UncompressedBuffer
    public void DecompressWorkItem(WorkItemDecompressSnesTraces workItemDecompressSnesTraces)
    {
        Debug.Assert(!workItemDecompressSnesTraces.WasDecompressed);

        AllocateUncompressedBuffer(workItemDecompressSnesTraces);
        var decompressedLength = 0;
        
        Debug.Assert(workItemDecompressSnesTraces.CompressedBuffer != null);
        Debug.Assert(workItemDecompressSnesTraces.UncompressedBuffer != null);
        if (workItemDecompressSnesTraces.CompressedBuffer == null || workItemDecompressSnesTraces.UncompressedBuffer == null)
            throw new InvalidOperationException("failed to allocate memory for work item buffers");

        using (var memory = new MemoryStream(workItemDecompressSnesTraces.CompressedBuffer))
        {
            using var inflater = new InflaterInputStream(memory);
            decompressedLength = inflater.Read(workItemDecompressSnesTraces.UncompressedBuffer, 0,
                workItemDecompressSnesTraces.UncompressedSize);
        }

        workItemDecompressSnesTraces.WasDecompressed = true;

        if (decompressedLength != workItemDecompressSnesTraces.UncompressedSize)
            throw new InvalidDataException("incorrect decompressed data size");

        // after this function, compressedWorkItem.CompressedBuffer is no longer needed.
        // but, leave it allocated so future runs can re-use that buffer.
    }

    public WorkItemSnesTrace ParseSnesTraceWorkItem(Stream stream, byte workItemId, byte workItemLen)
    {
        // given 2 bytes already read (in workItemId and workItemLen), read the rest of the SNES trace info from the input stream
        
        var workItem = AllocateWorkItem(workItemLen);
        if (workItem.Buffer == null)
            throw new InvalidOperationException("failed to allocate (or invalid) buffer for WorkItem");

        if (workItemId != 0xEE && workItemId != 0xEF)
            throw new InvalidDataException("Missing expected watermark from unzipped data");

        var abridgedFormat = workItemId == 0xEE;

        workItem.AbridgedFormat = abridgedFormat;

        var bytesRead = stream.Read(workItem.Buffer, 0, workItem.Buffer.Length);

        if (bytesRead != workItem.Buffer.Length)
            throw new InvalidDataException("Didn't read enough bytes from unzipped data");

        return workItem;
    }
}