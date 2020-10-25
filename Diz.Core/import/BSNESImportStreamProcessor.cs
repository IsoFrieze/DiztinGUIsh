using System.Collections.Generic;
using System.IO;
using System.Threading;
using Diz.Core.util;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace Diz.Core.import
{
    public class BsnesImportStreamProcessor
    {
        public struct CompressedWorkItems
        {
            public byte[] Bytes;
            public int UncompressedSize;
        }

        public struct WorkItem
        {
            public byte[] Buffer;
            public bool AbridgedFormat;
        }

        public CancellationTokenSource CancelToken { get; } = new CancellationTokenSource();

        public IEnumerable<CompressedWorkItems> GetCompressedWorkItems(Stream stream)
        {
            while (!CancelToken.IsCancellationRequested)
            {
                // perf: huge volume of data coming in.
                // need to get the data read from the server as fast as possible.
                // do minimal processing here then dump it over to the other thread for real processing.
                yield return ReadPacketFromStream(stream);
            }
        }

        private static CompressedWorkItems ReadPacketFromStream(Stream stream)
        {
            const int headerSize = 9;
            var buffer = Util.ReadNext(stream, headerSize, out _);
            if (buffer.Length != headerSize)
            {
                throw new InvalidDataException($"invalid header length for compressed data chunk");
            }

            if (buffer[0] != 'Z')
            {
                throw new InvalidDataException($"expected header byte of 'Z', got {buffer[0]} instead.");
            }

            var originalDataSizeBytes = ByteUtil.ByteArrayToInt32(buffer, 1);
            var compressedDataSize = ByteUtil.ByteArrayToInt32(buffer, 5);

            buffer = Util.ReadNext(stream, compressedDataSize, out _);
            if (buffer.Length != compressedDataSize)
            {
                throw new InvalidDataException(
                    $"compressed data: expected {compressedDataSize} bytes, only got {buffer.Length}");
            }

            return new CompressedWorkItems()
            {
                Bytes = buffer,
                UncompressedSize = originalDataSizeBytes
            };
        }

        public static IEnumerable<WorkItem> ProcessCompressedWorkItems(CompressedWorkItems compressedWorkItems)
        {
            using var stream = new MemoryStream(DecompressWorkItems(compressedWorkItems));

            foreach (var workItem in ReadWorkItems(stream))
            {
                yield return workItem;
            }
        }

        private static byte[] DecompressWorkItems(CompressedWorkItems compressedWorkItems)
        {
            var decompressedData = new byte[compressedWorkItems.UncompressedSize];
            var decompressedLength = 0;

            using (var memory = new MemoryStream(compressedWorkItems.Bytes))
            {
                using var inflater = new InflaterInputStream(memory);
                decompressedLength = inflater.Read(decompressedData, 0, decompressedData.Length);
            }

            if (decompressedLength != compressedWorkItems.UncompressedSize)
                throw new InvalidDataException("incorrect decompressed data size");

            return decompressedData;
        }

        private static IEnumerable<WorkItem> ReadWorkItems(Stream stream)
        {
            var header = new byte[2];
            while (stream.Read(header, 0, 2) == 2)
            {
                var id = header[0];
                var len = header[1];
                foreach (var workItem in ReadWorkItem(stream, id, len))
                {
                    yield return workItem;
                }
            }
        }

        private static IEnumerable<WorkItem> ReadWorkItem(Stream stream, byte workItemId, byte workItemLen)
        {
            if (workItemId != 0xEE && workItemId != 0xEF)
                throw new InvalidDataException("Missing expected watermark from unzipped data");

            var abridgedFormat = workItemId == 0xEE;

            var buffer = new byte[workItemLen];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);

            if (bytesRead != buffer.Length)
                throw new InvalidDataException("Didn't read enough bytes from unzipped data");

            yield return new WorkItem() {Buffer = buffer, AbridgedFormat = abridgedFormat};
        }
    }
}