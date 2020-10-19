using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Diz.Core.model;
using Diz.Core.util;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace Diz.Core.import
{
    public class BSNESTraceLogCapture
    {
        private CancellationTokenSource cancelToken;
        private BackgroundWorker socketWorker, dataWorker;

        struct Packet
        {
            public byte[] bytes;
            public int uncompressedSize;
        }

        private BlockingCollection<Packet> queue; // thread-safe
        public int QueueLength => queue?.Count ?? 0;

        public bool Running => dataWorker != null && socketWorker != null;

        public event EventHandler Finished;

        public delegate void ThreadErrorEvent(Exception e);
        public event ThreadErrorEvent Error;
        public bool Finishing { get; protected set; }

        // keep thread safety in mind for variables below this line

        private readonly ReaderWriterLockSlim importerLock = new ReaderWriterLockSlim();
        private BSNESTraceLogImporter importer;
        private BSNESTraceLogImporter.Stats cachedStats;
        private int bytesInQueue;
        
        public (BSNESTraceLogImporter.Stats, int) GetStats()
        {
            if (importer == null) 
                return (cachedStats, 0);

            var cachedBytesInQueue = 0;

            try
            {
                importerLock.EnterReadLock();
                cachedStats = importer.CurrentStats;
                cachedBytesInQueue = bytesInQueue;
            }
            finally
            {
                importerLock.ExitReadLock();
            }

            return (cachedStats, cachedBytesInQueue);
        }

        public void Start(Data data)
        {
            importer = new BSNESTraceLogImporter(data);

            cancelToken = new CancellationTokenSource();
            queue = new BlockingCollection<Packet>();
            bytesInQueue = 0;

            socketWorker = new BackgroundWorker();
            dataWorker = new BackgroundWorker();

            socketWorker.DoWork += SocketWorker_DoWork;
            dataWorker.DoWork += DataWorker_DoWork;

            socketWorker.WorkerSupportsCancellation = dataWorker.WorkerSupportsCancellation = true;

            socketWorker.RunWorkerCompleted += SocketWorker_RunWorkerCompleted;
            dataWorker.RunWorkerCompleted += DataWorker_RunWorkerCompleted;

            Thread.MemoryBarrier();

            socketWorker.RunWorkerAsync();
            dataWorker.RunWorkerAsync();
        }

        private void SocketWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var tcpClient = new TcpClient();
            var ipEndPoint = new IPEndPoint(IPAddress.Loopback, 27015);

            tcpClient.Connect(ipEndPoint);
            var networkStream = tcpClient.GetStream();

            while (!socketWorker.CancellationPending && !cancelToken.IsCancellationRequested)
            {
                // perf: huge volume of data coming in.
                // need to get the data read from the server as fast as possible.
                // do minimal processing here then dump it over to the other thread for real processing.
                ReadNextFromSocket(networkStream);
            }
        }

        private void ReadNextFromSocket(Stream networkStream)
        {
            const int headerSize = 9;
            var buffer = ReadNext(networkStream, headerSize, out var bytesRead);
            Debug.Assert(buffer.Length == headerSize);

            if (buffer[0] != 'Z') {
                throw new InvalidDataException($"expected header byte of 'Z', got {buffer[0]} instead.");
            }

            var originalDataSizeBytes = ByteUtil.ByteArrayToInt32(buffer, 1);
            var compressedDataSize = ByteUtil.ByteArrayToInt32(buffer, 5);

            buffer = ReadNext(networkStream, compressedDataSize, out bytesRead);
            Debug.Assert(buffer.Length == compressedDataSize);

            // add it compressed.
            queue.Add(new Packet() {bytes=buffer, uncompressedSize = originalDataSizeBytes});
            
            // this is just neat stats. it's optional, remove if performance becomes an issue (seems unlikely)
            try
            {
                importerLock.EnterWriteLock();
                bytesInQueue += compressedDataSize;
            }
            finally
            {
                importerLock.ExitWriteLock();
            }
        }

        private static byte[] ReadNext(Stream networkStream, int count, out int bytesRead)
        {
            var buffer = new byte[count];
            bytesRead = 0;
            var offset = 0;

            while (count > 0 && (bytesRead = networkStream.Read(buffer, offset, count)) > 0)
            {
                count -= bytesRead;
                offset += bytesRead;
            }

            if (count > 0)
                throw new EndOfStreamException();

            return buffer;
        }

        private void DataWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // TODO: we're officially CPU bound now.
            // probably should create multiple parallel workers
            // to take items off the compressed queue
            // and add them to an uncompressed queue

            while (!dataWorker.CancellationPending && !cancelToken.IsCancellationRequested)
            {
                try
                {
                    // TODO: use cancelToken here instead of timeout. (can't get it to work right now...)
                    if (!queue.TryTake(out var packet, 100))
                        continue;

                    var decompressedData = new byte[packet.uncompressedSize];
                    var decompressedLength = 0;
                    using (var memory = new MemoryStream(packet.bytes))
                    using (var inflater = new InflaterInputStream(memory))
                    {
                        decompressedLength = inflater.Read(decompressedData, 0, decompressedData.Length);
                    }
                    Debug.Assert(decompressedLength == packet.uncompressedSize);

                    using (var stream = new MemoryStream(decompressedData))
                    {
                        var header = new byte[2];
                        while (stream.Read(header, 0, 2) == 2)
                        {
                            var id = header[0];
                            var len = header[1];

                            if (id != 0xEE && id != 0xEF)
                                throw new InvalidDataException("Missing expected watermark from unzipped data");

                            var abridgedFormat = id == 0xEE;

                            var buffer = new byte[len];
                            var bytesRead = stream.Read(buffer, 0, buffer.Length);
                            if (bytesRead != buffer.Length)
                                throw new InvalidDataException("Didn't read enough bytes from unzipped data");

                            ProcessOneInstruction(buffer, abridgedFormat);
                        }
                    }

                    // optional, but nice stats.
                    try
                    {
                        importerLock.EnterWriteLock();
                        bytesInQueue -= packet.bytes.Length;
                    }
                    finally
                    {
                        importerLock.ExitWriteLock();
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void ProcessOneInstruction(byte[] buffer, bool abridgedFormat)
        {
            try
            {
                importerLock.EnterWriteLock();
                importer.ImportTraceLogLineBinary(buffer, abridgedFormat);
            }
            finally
            {
                importerLock.ExitWriteLock();
            }
        }

        public void SignalToStop()
        {
            Finishing = true;

            cancelToken.Cancel(false);
            socketWorker.CancelAsync();
            dataWorker.CancelAsync();
        }

        private void DataWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            dataWorker = null;
            OnAnyThreadFinished(e);
        }

        private void SocketWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            socketWorker = null;
            OnAnyThreadFinished(e);
        }

        private void OnAnyThreadFinished(RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
                OnThreadError(e.Error);

            SignalIfFinished();
        }

        private void SignalIfFinished()
        {
            if (dataWorker == null && socketWorker == null)
                OnFinished();
        }

        protected virtual void OnFinished()
        {
            Finishing = false;

            importer = null;

            cancelToken = null;
            queue = null;
            bytesInQueue = 0;
            socketWorker = dataWorker = null;

            Thread.MemoryBarrier();

            Finished?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnThreadError(Exception e)
        {
            Error?.Invoke(e);
        }
    }
}