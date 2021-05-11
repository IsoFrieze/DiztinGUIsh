// #define PROFILING

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Diz.Core.util;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace Diz.Core.import
{
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

        public void Return(ref T item)
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
        public class CompressedWorkItem : PoolItem
        {
            public byte[] Header;
            public const int HeaderSize = 9;

            public byte[] CompressedBuffer;
            public int CompressedSize;

            public byte[] UncompressedBuffer;
            public int UncompressedSize;
            public bool WasDecompressed;

            public byte[] TmpHeader;
            public readonly List<WorkItem> ListHeads = new();
        }

        public class WorkItem : PoolItem
        {
            public byte[] Buffer;
            public bool AbridgedFormat;
            public WorkItem Next;
        }

        private ObjPool<CompressedWorkItem> poolCompressedWorkItems;
        private ObjPool<WorkItem> poolWorkItems;

        public CancellationTokenSource CancelToken { get; private set; } = new CancellationTokenSource();

        public BsnesImportStreamProcessor(bool poolAllocations = true)
        {
            if (!poolAllocations)
                return;

            poolCompressedWorkItems = new ObjPool<CompressedWorkItem>();
            poolWorkItems = new ObjPool<WorkItem>();
        }

        public void Shutdown()
        {
            poolWorkItems = null;
            poolCompressedWorkItems = null;
            CancelToken = null;
        }

        public IEnumerable<CompressedWorkItem> GetCompressedWorkItems(Stream stream)
        {
            while (!CancelToken.IsCancellationRequested)
            {
                // perf: huge volume of data coming in.
                // need to get the data read from the server as fast as possible.
                // do minimal processing here then dump it over to the other thread for real processing.
                var compressedWorkItem = ReadPacketFromStream(stream);
                if (compressedWorkItem == null)
                    break;

                yield return compressedWorkItem;
            }
        }

        private CompressedWorkItem ReadPacketFromStream(Stream stream)
        {
#if PROFILING
            var mainSpan = Markers.EnterSpan("BSNES socket read");
#endif

            var item = AllocateCompressedWorkItem();

            try
            {
                Util.ReadNext(stream, item.Header, CompressedWorkItem.HeaderSize);
            }
            catch (EndOfStreamException)
            {
                FreeCompressedWorkItem(ref item);
                return null;
            }

#if PROFILING
            Markers.WriteFlag("initial read");
#endif

            if (item.Header.Length != CompressedWorkItem.HeaderSize)
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

        private CompressedWorkItem AllocateCompressedWorkItem()
        {
            var item = AllocCompressedWorkItem();

            item.CompressedSize = item.UncompressedSize = 0;
            item.WasDecompressed = false;

            if (item.Header != null && item.Header.Length == CompressedWorkItem.HeaderSize)
                return item;

            item.Header = new byte[CompressedWorkItem.HeaderSize];
            return item;
        }

        private CompressedWorkItem AllocCompressedWorkItem()
        {
            return poolCompressedWorkItems == null ? new CompressedWorkItem() : poolCompressedWorkItems.Get();
        }

        private WorkItem AllocWorkItem()
        {
            return poolWorkItems == null ? new WorkItem() : poolWorkItems.Get();
        }

        public void FreeCompressedWorkItem(ref CompressedWorkItem compressedItem)
        {
            // don't kill the big buffers. main point of this pool is to hopefully re-use them later.

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

        public void FreeWorkItem(ref WorkItem workItem)
        {
            if (workItem == null)
                return;

            // don't kill the big buffers. main point of this pool is to hopefully re-use them later.

            workItem.Next = null;

            poolWorkItems?.Return(ref workItem);
        }

        private WorkItem AllocateWorkItem(byte workItemLen)
        {
            var workItem = AllocWorkItem();

            // turn this on if you ever think you have a memroy alloc issue
            #if SERIOUS_DEBUG_MEM_VERIFY
            if (workItem.IsFree || workItem.Next != null)
            {
                Debugger.Break();
            }
            #endif

            workItem.AbridgedFormat = false;

            // almost every item re-used from the pool should have the correct buffer size already.
            // this size is usually fixed, so ok to check for exact len.
            if (workItem.Buffer != null && workItem.Buffer.Length == workItemLen)
                return workItem;

            workItem.Buffer = new byte[workItemLen];

            return workItem;
        }

        private static void AllocateUncompressedBuffer(CompressedWorkItem compressedWorkItem)
        {
            Debug.Assert(compressedWorkItem.UncompressedSize != 0);
            // remember, our goal is to reduce the # of allocations done for compressedWorkItem.UncompressedBuffer
            // CompressedWorkItems are cached as a pool, and if our buffer size is large enough, we can
            // avoid a re-allocation.

            if (compressedWorkItem.UncompressedBuffer != null &&
                compressedWorkItem.UncompressedBuffer.Length >= compressedWorkItem.UncompressedSize)
                return;

            // go a little more than we need to avoid
            // re-use needing to re-allocate.
            // works because our uncompressedsize shouldn't change much
            const float unscientificMultiplierGuess = 1.2f;

            var size = (int) (compressedWorkItem.UncompressedSize * unscientificMultiplierGuess);
            compressedWorkItem.UncompressedBuffer = new byte[size];
        }

        public void DecompressWorkItem(CompressedWorkItem compressedWorkItem)
        {
            Debug.Assert(!compressedWorkItem.WasDecompressed);

            AllocateUncompressedBuffer(compressedWorkItem);
            var decompressedLength = 0;

            using (var memory = new MemoryStream(compressedWorkItem.CompressedBuffer))
            {
                using var inflater = new InflaterInputStream(memory);
                decompressedLength = inflater.Read(compressedWorkItem.UncompressedBuffer, 0,
                    compressedWorkItem.UncompressedSize);
            }

            compressedWorkItem.WasDecompressed = true;

            if (decompressedLength != compressedWorkItem.UncompressedSize)
                throw new InvalidDataException("incorrect decompressed data size");

            // after this function, compressedWorkItem.CompressedBuffer is no longer needed.
            // but, leave it allocated so future runs can re-use that buffer.
        }

        public WorkItem ReadWorkItem(Stream stream, byte workItemId, byte workItemLen)
        {
            var workItem = AllocateWorkItem(workItemLen);

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
}