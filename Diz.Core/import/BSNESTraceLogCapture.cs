using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Diz.Core.import;
using Diz.Core.model;

namespace DiztinGUIsh.window.dialog
{
    public class BSNESTraceLogCapture
    {
        private CancellationTokenSource cancelToken;
        private BackgroundWorker socketWorker, dataWorker;

        private BlockingCollection<byte[]> queue; // thread-safe
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
        public BSNESTraceLogImporter.Stats GetStats()
        {
            if (importer == null) 
                return cachedStats;

            try
            {
                importerLock.EnterReadLock();
                cachedStats = importer.CurrentStats;
            }
            finally
            {
                importerLock.ExitReadLock();
            }

            return cachedStats;
        }

        public void Start(Data data)
        {
            importer = new BSNESTraceLogImporter(data);

            cancelToken = new CancellationTokenSource();
            queue = new BlockingCollection<byte[]>();

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
            var buffer = ReadNext(networkStream, 2, out var bytesRead);
            Debug.Assert(buffer.Length == 2);

            const byte expectedWatermark = 0xEE;
            if (buffer[0] != expectedWatermark) {
                throw new InvalidDataException($"expected header of 0xEE, got {buffer[0]} instead.");
            }

            int amountToRead = buffer[1];
            Debug.Assert(amountToRead == 21);

            buffer = ReadNext(networkStream, amountToRead, out bytesRead);
            queue.Add(buffer);
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
            while (!dataWorker.CancellationPending && !cancelToken.IsCancellationRequested)
            {
                try
                {
                    // TODO: use cancelToken here instead of timeout. (can't get it to work right now...)
                    if (!queue.TryTake(out var bytes, 100))
                        continue;
                    
                    try
                    {
                        importerLock.EnterWriteLock();
                        importer.ImportTraceLogLineBinary(bytes);
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