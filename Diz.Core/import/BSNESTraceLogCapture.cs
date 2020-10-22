using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.import
{
    // TODO: can probably replace this better with Dataflow TPL, investigate

    public class BSNESTraceLogCapture
    {
        private WorkerTaskManager taskManager;
        private BSNESImportStreamProcessor streamProcessor;

        private readonly ReaderWriterLockSlim importerLock = new ReaderWriterLockSlim();
        private BSNESTraceLogImporter importer;
        private int bytesToProcess = 0;
        private int compressedBlocksToProcess = 0;
        private BSNESTraceLogImporter.Stats cachedStats;

        public bool Running { get; protected set; }

        public int BytesToProcess => bytesToProcess;
        public int BlocksToProcess => compressedBlocksToProcess;
        public bool Finishing => streamProcessor?.CancelToken?.IsCancellationRequested ?? false;

        public void Run(Data data)
        {
            Setup(data);
            try
            {
                Main();
                taskManager.WaitForAllTasksToComplete();
            }
            finally
            {
                Shutdown();
            }
        }

        private void Shutdown()
        {
            taskManager = null;
            streamProcessor = null;
            importer = null;
            Running = false;
        }

        private void Setup(Data data)
        {
            Running = true;
            importer = new BSNESTraceLogImporter(data);
            streamProcessor = new BSNESImportStreamProcessor();
            taskManager = new WorkerTaskManager();
        }

        private static NetworkStream Connect()
        {
            var tcpClient = new TcpClient();
            //await tcpClient.ConnectAsync(IPAddress.Loopback, 27015);
            //return tcpClient.GetStream();
            tcpClient.Connect(IPAddress.Loopback, 27015);
            return tcpClient.GetStream();
        }

        private void Main()
        {
            var networkStream = Connect();
            ProcessStreamData(networkStream);
        }

        private void ProcessStreamData(Stream networkStream)
        {
            using var enumerator = streamProcessor.GetCompressedWorkItems(networkStream).GetEnumerator();
            while (!streamProcessor.CancelToken.IsCancellationRequested && enumerator.MoveNext())
            {
                var compressedItems = enumerator.Current;
                taskManager.Run(() => ProcessCompressedWorkItem(compressedItems));
                States_MarkQueued(compressedItems);
            }
        }

        private async void ProcessCompressedWorkItem(BSNESImportStreamProcessor.CompressedWorkItems compressedItems)
        {
            // tune this as needed.
            // we want parallel jobs going, but, we don't want too many of them at once.
            // average # workItems per CompressedWorkItem is like 12K currently.
            const int numItemsPerTask = 6000;

            using var enumerator = BSNESImportStreamProcessor.ProcessCompressedWorkItems(compressedItems).GetEnumerator();

            bool keepGoing;
            var itemsRemainingBeforeSend = numItemsPerTask;
            var subTasks = new List<Task>();
            var workItemsForThisTask = new List<BSNESImportStreamProcessor.WorkItem>();

            do
            {
                var endOfList = !enumerator.MoveNext();
                keepGoing = !streamProcessor.CancelToken.IsCancellationRequested && !endOfList;

                if (!endOfList)
                {
                    workItemsForThisTask.Add(enumerator.Current);
                    itemsRemainingBeforeSend--;
                }

                var shouldSendNow = !keepGoing || itemsRemainingBeforeSend == 0;
                if (!shouldSendNow) 
                    continue;

                var workItemsCopy = new List<BSNESImportStreamProcessor.WorkItem>(workItemsForThisTask);
                subTasks.Add(taskManager.Run(() =>
                {
                    ProcessWorkItems(workItemsCopy);
                }));

                itemsRemainingBeforeSend = numItemsPerTask;
                workItemsForThisTask.Clear();
            } while (keepGoing);

            await Task.WhenAll(subTasks);
            Stats_MarkCompleted(compressedItems);
        }

        private void ProcessWorkItems(IEnumerable<BSNESImportStreamProcessor.WorkItem> workItemsForThisTask)
        {
            foreach (var workItem in workItemsForThisTask)
            {
                ProcessWorkItem(workItem);
            }
        }

        // this is just neat stats. it's optional, remove if performance becomes an issue (seems unlikely)
        private void States_MarkQueued(BSNESImportStreamProcessor.CompressedWorkItems compressedItems)
        {
            try
            {
                importerLock.EnterWriteLock();
                bytesToProcess += compressedItems.Bytes.Length;
                compressedBlocksToProcess++;
            }
            finally
            {
                importerLock.ExitWriteLock();
            }
        }

        private void Stats_MarkCompleted(BSNESImportStreamProcessor.CompressedWorkItems compressedItems)
        {
            try
            {
                importerLock.EnterWriteLock();
                bytesToProcess -= compressedItems.Bytes.Length;
                compressedBlocksToProcess--;
            }
            finally
            {
                importerLock.ExitWriteLock();
            }
        }

        private void ProcessWorkItem(BSNESImportStreamProcessor.WorkItem workItem)
        {
            try
            {
                // definitely hitting lock contention.
                importerLock.EnterWriteLock();
                importer.ImportTraceLogLineBinary(workItem.Buffer, workItem.AbridgedFormat);
            }
            finally
            {
                importerLock.ExitWriteLock();
            }
        }

        public void SignalToStop()
        {
            streamProcessor?.CancelToken?.Cancel();
            taskManager.StartFinishing();
        }

        public (BSNESTraceLogImporter.Stats stats, int bytesToProcess) GetStats()
        {
            if (importer == null)
                return (cachedStats, 0);

            var cachedBytesInQueue = 0;

            try
            {
                importerLock.EnterReadLock();
                cachedStats = importer.CurrentStats;
                cachedBytesInQueue = bytesToProcess;
            }
            finally
            {
                importerLock.ExitReadLock();
            }

            return (cachedStats, cachedBytesInQueue);
        }
    }
}