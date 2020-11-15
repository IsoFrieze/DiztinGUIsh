// #define PROFILING

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Diz.Core.model;
using Diz.Core.util;
#if PROFILING
using Microsoft.ConcurrencyVisualizer.Instrumentation;
#endif


namespace Diz.Core.import
{
    // TODO: can probably replace this better with Dataflow TPL, investigate
    // Caution: This class is heavily multi-threaded, pay attention to locking/concurrency issues.

    public class BsnesTraceLogCapture
    {
        private IWorkerTaskManager taskManager;
        private BsnesImportStreamProcessor streamProcessor;
        
        private BsnesTraceLogImporter importer;
        
        private int statsBytesToProcess = 0;
        private int statsCompressedBlocksToProcess = 0;
        private BsnesTraceLogImporter.Stats cachedStats;

        public bool Running { get; protected set; }

        public int StatsBytesToProcess => statsBytesToProcess;
        public int BlocksToProcess => statsCompressedBlocksToProcess;
        public bool Finishing => streamProcessor?.CancelToken?.IsCancellationRequested ?? false;

        public void Run(Data data)
        {
            Setup(data);
            try
            {
                Main();
                taskManager.StartFinishing();
                taskManager.WaitForAllTasksToComplete();
            }
            finally
            {
                Shutdown();
            }
        }

        private void Shutdown()
        {
            streamProcessor.Shutdown();
            
            streamProcessor = null;
            taskManager = null;
            importer = null;
            
            Running = false;
        }

        private void Setup(Data data)
        {
            Running = true;
            streamProcessor = new BsnesImportStreamProcessor();
            taskManager = CreateWorkerTaskManager();
            importer = CreateTraceLogImporter(data);
        }

        protected virtual BsnesTraceLogImporter CreateTraceLogImporter(Data data)
        {
            // multi-threaded version
            // return new BsnesTraceLogImporter(data, streamProcessor.CancelToken.Token, taskManager);
            return new BsnesTraceLogImporter(data);
        }

        protected virtual IWorkerTaskManager CreateWorkerTaskManager()
        {
            return new WorkerTaskManager();
        }

        protected virtual Stream GetInputStream()
        {
            return OpenNetworkStream();
        }

        private static NetworkStream OpenNetworkStream(IPAddress ip = null, int port = 27015)
        {
            var tcpClient = new TcpClient();
            tcpClient.Connect(ip ?? IPAddress.Loopback, port);
            return tcpClient.GetStream();
        }

        protected virtual void Main()
        {
            #if PROFILING
            var mainSpan = Markers.EnterSpan("BSNES Main");
            #endif

            var networkStream = GetInputStream();
            ProcessStreamData(networkStream);

            #if PROFILING
            mainSpan.Leave();
            #endif
        }

        private const int maxNumCompressedItemsToProcess = -1; // debug only.

        // set a limit for the max# of worker tasks allowed to operate on the compressed data. tweak this number as needed.
        // this is purely for throttling and not for thread safety, otherwise # of Tasks will run out of control.
        private SemaphoreSlim compressedWorkersLimit = new SemaphoreSlim(4,4);
        private SemaphoreSlim uncompressedWorkersLimit = new SemaphoreSlim(4, 4);

        private void ProcessStreamData(Stream networkStream)
        {
            var count = 0;
            
            using var enumerator = streamProcessor.GetCompressedWorkItems(networkStream).GetEnumerator();
            while (!streamProcessor.CancelToken.IsCancellationRequested && enumerator.MoveNext())
            {
                var compressedItem = enumerator.Current;

                // could put inside the task to start the task sooner after we hit this.
                // doing it here will limit the # of tasks created and waiting, and the # of compressedItems active at once,
                // which can run away very quickly. 
                     
                                                    
                taskManager.Run(() =>
                {
                    try
                    {
                        compressedWorkersLimit.Wait(streamProcessor.CancelToken.Token);
                        try
                        {
                            ProcessCompressedWorkItem(compressedItem);
                        }
                        finally
                        {
                            compressedWorkersLimit.Release();
                        }
                    } catch (OperationCanceledException) {
                        Debug.WriteLine("Cancelling...");
                        // NOP
                    }
                });
                Stats_MarkQueued(compressedItem);

                count++;
                if (maxNumCompressedItemsToProcess != -1 && count >= maxNumCompressedItemsToProcess)
                    return;
            }
            
            Trace.WriteLine($"Processed {count} compressed work items.");
        }

        private async void ProcessCompressedWorkItem(BsnesImportStreamProcessor.CompressedWorkItem compressedItem)
        {    
            #if PROFILING
            var mainSpan = Markers.EnterSpan("BSNES ProcessCompressedWorkItem");
            #endif

            DecompressWorkItem(compressedItem);
            PartitionWorkItemQueue(compressedItem);
            var subTasks = DispatchWorkersForCompressedWorkItem(compressedItem);
            var statsBytesCompleted = compressedItem.CompressedSize;
            streamProcessor.FreeCompressedWorkItem(compressedItem);
            await Task.WhenAll(subTasks);

            Stats_MarkCompleted(statsBytesCompleted);

            #if PROFILING
            mainSpan.Leave();
            #endif
        }

        private IEnumerable<Task> DispatchWorkersForCompressedWorkItem(BsnesImportStreamProcessor.CompressedWorkItem compressedItem)
        {
            var subTasks = new List<Task>(capacity: compressedItem.listHeads.Count);
            for (var i = 0; i < compressedItem.listHeads.Count; ++i)
            {
                var workItemListHead = compressedItem.listHeads[i];
                
                subTasks.Add(taskManager.Run(() =>
                {
                    // this subtask shouldn't have any references to the compressedWorkItem here, we want to be fully
                    // separated so that we can free it below immediately after.
                    try
                    {
                        uncompressedWorkersLimit.Wait(streamProcessor.CancelToken.Token);
                        try
                        {
                            ProcessWorkItemsLinkedList(workItemListHead);
                        }
                        finally
                        {
                            uncompressedWorkersLimit.Release();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine("Cancelling2...");
                        // NOP
                    }
                }));

                compressedItem.listHeads[i] = null; // remove the reference.
            }

            return subTasks;
        }

        private void PartitionWorkItemQueue(BsnesImportStreamProcessor.CompressedWorkItem compressedItem)
        {
            Debug.Assert(compressedItem.wasDecompressed);
            
            using var stream = new MemoryStream(compressedItem.UncompressedBuffer, 0, compressedItem.UncompressedSize);
            compressedItem.tmpHeader ??= new byte[2];
            
            // tune this as needed.
            // we want parallel jobs going, but, we don't want too many of them at once.
            // average # workItems per CompressedWorkItem is like 12K currently.
            const int numItemsPerTask = 6000;
            bool keepGoing;
            var itemsRemainingBeforeEnd = numItemsPerTask;

            Debug.Assert(compressedItem.listHeads != null && compressedItem.listHeads.Count == 0);
            
            BsnesImportStreamProcessor.WorkItem currentHead = null;
            BsnesImportStreamProcessor.WorkItem currentItem = null;

            do
            {
                var nextItem = ReadNextWorkItem(stream, compressedItem.tmpHeader);

                if (nextItem != null)
                {
                    Debug.Assert(nextItem.next == null);

                    if (currentHead == null)
                    {
                        currentHead = nextItem;
                        Debug.Assert(currentItem == null);
                    }
                    else
                    {
                        currentItem.next = nextItem;
                    }
                    currentItem = nextItem;

                    itemsRemainingBeforeEnd--;
                }
                
                keepGoing = !streamProcessor.CancelToken.IsCancellationRequested && nextItem != null;
                var endOfPartition = !keepGoing || itemsRemainingBeforeEnd == 0;
                if (!endOfPartition)
                    continue;

                // finish list
                if (currentHead != null)
                {
                    Debug.Assert(currentItem.next == null);
                    compressedItem.listHeads.Add(currentHead);
                }
                
                // reset list
                currentHead = currentItem = null;
                itemsRemainingBeforeEnd = numItemsPerTask;
            } while (keepGoing);
        }

        private BsnesImportStreamProcessor.WorkItem ReadNextWorkItem(Stream stream, byte[] header)
        {
            if (stream.Read(header, 0, 2) != 2) 
                return null;
            
            var workItemId = header[0];
            var workItemLen = header[1];
            return streamProcessor.ReadWorkItem(stream, workItemId, workItemLen);
        }

        private void DecompressWorkItem(BsnesImportStreamProcessor.CompressedWorkItem compressedItem)
        {
            Debug.Assert(compressedItem.CompressedBuffer != null);
            Debug.Assert(compressedItem.UncompressedSize != 0);
            Debug.Assert(compressedItem.UncompressedSize != 0);
            Debug.Assert(!compressedItem.wasDecompressed);
            
            streamProcessor.DecompressWorkItem(compressedItem);
            
            Debug.Assert(compressedItem.UncompressedBuffer != null);
            Debug.Assert(compressedItem.wasDecompressed);
        }

        private void ProcessWorkItemsLinkedList(BsnesImportStreamProcessor.WorkItem workItemListHead)
        {
            #if PROFILING
            var mainSpan = Markers.EnterSpan("BSNES ProcessWorkItems");
            #endif

            // iterate linked list
            var current = workItemListHead;
            while (current != null) { 
                ProcessWorkItem(current);
                var next = current.next;
                streamProcessor.FreeWorkItem(current);
                current = next;
            }
            
            #if PROFILING
            mainSpan.Leave();
            #endif
        }

        // this is just neat stats. it's optional, remove if performance becomes an issue (seems unlikely)
        private void Stats_MarkQueued(BsnesImportStreamProcessor.CompressedWorkItem compressedItem)
        {
            Interlocked.Add(ref statsBytesToProcess, compressedItem.CompressedSize);
            Interlocked.Increment(ref statsCompressedBlocksToProcess);
        }

        private void Stats_MarkCompleted(int bytesCompleted)
        {
            Interlocked.Add(ref statsBytesToProcess, -bytesCompleted);
            Interlocked.Decrement(ref statsCompressedBlocksToProcess);
        }

        private void ProcessWorkItem(BsnesImportStreamProcessor.WorkItem workItem)
        {
            #if PROFILING
            var mainSpan = Markers.EnterSpan("BSNES ProcessWorkItem");
            #endif

            // this importer call is thread-safe, so we don't need to do our own locking 
            importer.ImportTraceLogLineBinary(workItem.Buffer, workItem.AbridgedFormat);

#if PROFILING
            mainSpan.Leave();
            #endif
        }

        public void SignalToStop()
        {
            streamProcessor?.CancelToken?.Cancel();
            taskManager.StartFinishing();
        }

        public (BsnesTraceLogImporter.Stats stats, int bytesToProcess) GetStats()
        {
            if (importer == null)
                return (cachedStats, 0);

            var cachedBytesInQueue = 0;
            
            // this is thread-safe and will make a copy for us.
            cachedStats = importer.CurrentStats;
            
            cachedBytesInQueue = statsBytesToProcess;
            
            return (cachedStats, cachedBytesInQueue);
        }
    }

    // mostly for testing, though we could use it to speed up importing too.
    public class BsnesTraceLogFileCapture : BsnesTraceLogCapture
    {
        private readonly byte[] bytes;

        public BsnesTraceLogFileCapture(string dataFile) : base()
        {
            bytes = File.ReadAllBytes(dataFile);
        }

        protected override Stream GetInputStream()
        {
            return new MemoryStream(bytes);
        }
        
        protected override IWorkerTaskManager CreateWorkerTaskManager()
        {
            return base.CreateWorkerTaskManager(); // regular version (multithreaded)
            // return new WorkerTaskManagerSynchronous(); // single-threaded version (for testing/debug only)
        }
        
        protected override BsnesTraceLogImporter CreateTraceLogImporter(Data data)
        {
            // single-threaded version
            return new BsnesTraceLogImporter(data);
        }
    }

    // debug only. same as above, but, run it a bunch of times mostly for performance
    // testing and memory allocation reasons.
    public class BsnesTraceLogDebugBenchmarkFileCapture : BsnesTraceLogFileCapture
    {
        private readonly int numTimes = 1;

        // for benchmarking, these are called immediate before and after Main(), which represents the bulk of the CPU work.
        // if optimizing, making these work faster is a good place to start.
        public Action OnStart { get; set; }
        public Action OnStop { get; set; }
        
        protected override void Main()
        {
            OnStart?.Invoke();
            
            for (var i = 0; i < numTimes; ++i)
                base.Main();

            OnStop?.Invoke();
        }

        public BsnesTraceLogDebugBenchmarkFileCapture(string dataFile, int numTimes) : base(dataFile)
        {
            this.numTimes = numTimes;
        }
    }
}