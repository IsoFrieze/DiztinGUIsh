// #define PROFILING

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diz.Core.util;
using Diz.Cpu._65816;
#if PROFILING
using Microsoft.ConcurrencyVisualizer.Instrumentation;
#endif


namespace Diz.Import.bsnes.tracelog;

// TODO: can probably replace this better with Dataflow TPL or newer async/await. investigate
// Caution: This class is heavily multi-threaded, pay attention to locking/concurrency issues.
public class BsnesTraceLogCapture
{
    public bool Running { get; private set; }
    
    private readonly IWorkerTaskManager taskManager;
    private readonly BsnesImportStreamProcessor streamProcessor;
    private readonly BsnesTraceLogImporter importer;
    
    private int statsBytesToProcess;
    private int statsCompressedBlocksToProcess;
    private BsnesTraceLogImporter.Stats cachedStats;
    
    public int BlocksToProcess => statsCompressedBlocksToProcess;
    public bool Finishing => streamProcessor.CancelToken.IsCancellationRequested;

    public BsnesTraceLogCapture(ISnesData data)
    {
        Running = true;
        streamProcessor = new BsnesImportStreamProcessor();
        
        // taskManager = new WorkerTaskManagerSynchronous(); // single-threaded version (for testing/debug only)
        taskManager = new WorkerTaskManager(); // multi-threaded version
        
        importer = new BsnesTraceLogImporter(data);
    }
    
    public void Run()
    {
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
        Running = false;
    }

    protected virtual Stream? GetInputStream()
    {
        return OpenNetworkStream();
    }

    private static NetworkStream? OpenNetworkStream(IPAddress? ip = null, int port = 27015)
    {
        var tcpClient = new TcpClient();

        var remoteIp = ip;
        if (ip == null)
        {
            // weirdly, it seems we can't just use IPAddress.Loopback anymore because it resolves to a weird IP
            // that doesn't always work.  we'll DNS lookup localhost instead
            var localhostAddresses = Dns.GetHostAddresses("localhost");
            if (localhostAddresses.Length > 0)
            {
                remoteIp = localhostAddresses[0]; // just pick the first one.
            }
        }

        if (remoteIp == null)
            return null;
        
        tcpClient.Connect(remoteIp, port);
        return tcpClient.GetStream();
    }

    protected virtual void Main()
    {
        #if PROFILING
        var mainSpan = Markers.EnterSpan("BSNES Main");
        #endif

        var networkStream = GetInputStream();
        
        // process incoming stream data until there's none left or we cancel
        ProcessStreamData(networkStream);

        // finally, copy any comments generated into snesData
        importer.CopyTempGeneratedCommentsIntoMainSnesData();

#if PROFILING
        mainSpan.Leave();
#endif
    }

    private const int MaxNumCompressedItemsToProcess = -1; // debug only.

    // set a limit for the max# of worker tasks allowed to operate on the compressed data. tweak this number as needed.
    // this is purely for throttling and not for thread safety, otherwise # of Tasks will run out of control.
    private readonly SemaphoreSlim compressedWorkersLimit = new(4,4);
    private readonly SemaphoreSlim uncompressedWorkersLimit = new(4, 4);

    // these can be modified as the trace is happening:
    public struct TraceLogCaptureSettings
    {
        public bool RemoveTracelogLabels { get; set; } = false;

        public bool AddTracelogLabel { get; set; } = false;

        public bool CaptureLabelsOnly { get; set; } = false;

        public string CommentTextToAdd { get; set; } = "";
        

        public TraceLogCaptureSettings()
        {
        }
    }

    public TraceLogCaptureSettings CaptureSettings { get; set; } = new();

    private void ProcessStreamData(Stream? networkStream)
    {
        if (streamProcessor == null || taskManager == null)
            throw new InvalidOperationException("stream processor and task manager must not be null");
        
        var count = 0;
        
        using var enumWorkItemSnesTraces = streamProcessor.GetCompressedWorkItems(networkStream).GetEnumerator();
        while (streamProcessor.CancelToken is { IsCancellationRequested: false } && enumWorkItemSnesTraces.MoveNext())
        {
            var workItemSnesTraces = enumWorkItemSnesTraces.Current;

            // could put this processing handler inside the task to start the task sooner after we hit this.
            // doing it here will limit the # of tasks created and waiting, and the # of compressedItems active at once,
            // which can run away very quickly.
            
            // first, let's capture the settings as they were at the TIME OF QUEUEING so when they are processed later,
            // we'll use these settings even if they've since changed.
            workItemSnesTraces.CaptureSettings = CaptureSettings;
            
            taskManager.Run(() =>
            {
                try
                {
                    compressedWorkersLimit.Wait(streamProcessor.CancelToken.Token);
                    try
                    {
                        ProcessCompressedSnesTracesWorkItem(workItemSnesTraces);
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
            Stats_MarkQueued(workItemSnesTraces);

            count++;
            if (MaxNumCompressedItemsToProcess != -1 && count >= MaxNumCompressedItemsToProcess)
                return;
        }
        
        Trace.WriteLine($"Processed {count} compressed work items.");
    }

    private async void ProcessCompressedSnesTracesWorkItem(BsnesImportStreamProcessor.WorkItemDecompressSnesTraces? workItemSnesTraces)
    {
        #if PROFILING
        var mainSpan = Markers.EnterSpan("BSNES ProcessCompressedWorkItem");
        #endif
        
        Debug.Assert(workItemSnesTraces != null);
        Debug.Assert(streamProcessor != null);

        // 1. take the compressed buffer in this work item and decompress it
        DecompressSnesBuffers(workItemSnesTraces);
        
        // 2. parse the SNES trace data in the newly uncompressed buffer
        // this uncompressed buffer contains thousands of individual CPU instruction trace data for SNES instructions that BSNES just executed
        // 
        // we'll divvy up the firehose of data into several queues for processing  
        CreateSnesTraceWorkQueues(workItemSnesTraces);
        
        // with our work queues built, fire off multiple parallel tasks to chew on the trace data.
        // these threads will parse the trace info and modify the Diz project based on the current tracelog capture settings
        var subTasks = DispatchWorkersForSnesTraceProcessing(workItemSnesTraces);
        
        var statsBytesCompleted = workItemSnesTraces.CompressedSize;
        
        // as soon as we've queued things up, we can free and re-use this workitem.
        // important to return our item to the pool ASAP so it can be re-used and the app doesn't run out of memory
        streamProcessor.FreeCompressedWorkItem(ref workItemSnesTraces);
        
        // wait for all workers to finish chewing on their SNES traces
        await Task.WhenAll(subTasks);

        // all workers now done
        Stats_MarkCompleted(statsBytesCompleted);

        #if PROFILING
        mainSpan.Leave();
        #endif
    }

    private IEnumerable<Task> DispatchWorkersForSnesTraceProcessing(BsnesImportStreamProcessor.WorkItemDecompressSnesTraces itemDecompressSnesTraces)
    {
        if (itemDecompressSnesTraces.ListHeads == null)
            throw new InvalidDataException("Expected non-null ListHeads for compressed work item dispatch");
        
        Debug.Assert(streamProcessor != null);

        // make a COPY of this struct so threads get the copy
        var captureSettings = itemDecompressSnesTraces.CaptureSettings;
        
        var subTasks = new List<Task>(capacity: itemDecompressSnesTraces.ListHeads.Count);
        for (var i = 0; i < itemDecompressSnesTraces.ListHeads.Count; ++i)
        {
            var workItemListHead = itemDecompressSnesTraces.ListHeads[i];
            if (workItemListHead == null)
                continue;
            
            subTasks.Add(taskManager.Run(() =>
            {
                // important: avoid passing the worker thread any references to itemDecompressSnesTraces here.
                // we want to be fully separated from that so as soon as this task starts,
                // we can immediately re-use itemDecompressSnesTraces
                try
                {
                    uncompressedWorkersLimit.Wait(streamProcessor.CancelToken.Token);
                    try
                    {
                        ProcessWorkItemsLinkedList(workItemListHead, captureSettings);
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

            itemDecompressSnesTraces.ListHeads[i] = null; // remove the reference.
        }

        return subTasks;
    }

    private void CreateSnesTraceWorkQueues(BsnesImportStreamProcessor.WorkItemDecompressSnesTraces workItemSnesTraces)
    {
        Debug.Assert(workItemSnesTraces.WasDecompressed);
        Debug.Assert(workItemSnesTraces.UncompressedBuffer != null);
        
        using var stream = new MemoryStream(workItemSnesTraces.UncompressedBuffer, 0, workItemSnesTraces.UncompressedSize);
        workItemSnesTraces.ScratchBuffer ??= new byte[2];
        
        // tune this as needed.
        // we want parallel jobs going, but, we don't want too many of them at once.
        // average # workItems per CompressedWorkItem is like 12K currently.
        const int numItemsPerTask = 6000;
        bool keepGoing;
        var itemsRemainingBeforeEnd = numItemsPerTask;

        Debug.Assert(workItemSnesTraces.ListHeads != null && workItemSnesTraces.ListHeads.Count == 0);
        
        BsnesImportStreamProcessor.WorkItemSnesTrace? currentHead = null;
        BsnesImportStreamProcessor.WorkItemSnesTrace? currentItem = null;

        // read all the SNES traces from the now-uncompressed buffer
        do
        {
            var nextItem = ParseNextSnesTrace(stream, workItemSnesTraces.ScratchBuffer);

            if (nextItem != null)
            {
                Debug.Assert(nextItem.Next == null);

                if (currentHead == null)
                {
                    currentHead = nextItem;
                    Debug.Assert(currentItem == null);
                }
                else
                {
                    Debug.Assert(currentItem != null);
                    currentItem.Next = nextItem;
                }
                currentItem = nextItem;

                itemsRemainingBeforeEnd--;
            }
            
            keepGoing = streamProcessor.CancelToken.IsCancellationRequested == false && nextItem != null;
            var endOfPartition = !keepGoing || itemsRemainingBeforeEnd == 0;
            if (!endOfPartition)
                continue;

            // finish list
            if (currentHead != null)
            {
                Debug.Assert(currentItem is { Next: null });
                workItemSnesTraces.ListHeads.Add(currentHead);
            }
            
            // reset list
            currentHead = currentItem = null;
            itemsRemainingBeforeEnd = numItemsPerTask;
        } while (keepGoing);
    }

    private BsnesImportStreamProcessor.WorkItemSnesTrace? ParseNextSnesTrace(Stream stream, byte[] header)
    {
        if (stream.Read(header, 0, 2) != 2) 
            return null;
        
        var workItemId = header[0];
        var workItemLen = header[1];
        return streamProcessor.ParseSnesTraceWorkItem(stream, workItemId, workItemLen);
    }

    private void DecompressSnesBuffers(BsnesImportStreamProcessor.WorkItemDecompressSnesTraces itemDecompressSnesTraces)
    {
        Debug.Assert(itemDecompressSnesTraces.CompressedBuffer != null);
        Debug.Assert(itemDecompressSnesTraces.UncompressedSize != 0);
        Debug.Assert(itemDecompressSnesTraces.UncompressedSize != 0);
        Debug.Assert(!itemDecompressSnesTraces.WasDecompressed);
        
        streamProcessor.DecompressWorkItem(itemDecompressSnesTraces);
        
        Debug.Assert(itemDecompressSnesTraces.UncompressedBuffer != null);
        Debug.Assert(itemDecompressSnesTraces.WasDecompressed);
    }

    private void ProcessWorkItemsLinkedList(BsnesImportStreamProcessor.WorkItemSnesTrace workItemSnesTrace, in TraceLogCaptureSettings captureSettings)
    {
        // performance critical function. be cautious when making changes
        
        #if PROFILING
        var mainSpan = Markers.EnterSpan("BSNES ProcessWorkItems");
        #endif

        // iterate linked list
        var current = workItemSnesTrace;
        while (current != null) { 
            ProcessWorkItemSnesTrace(current, in captureSettings);
            var next = current.Next;
            streamProcessor.FreeWorkItem(ref current);
            current = next;
        }
        
        #if PROFILING
        mainSpan.Leave();
        #endif
    }

    // this is just neat stats. it's optional, remove if performance becomes an issue (seems unlikely)
    private void Stats_MarkQueued(BsnesImportStreamProcessor.WorkItemDecompressSnesTraces itemDecompressSnesTraces)
    {
        Interlocked.Add(ref statsBytesToProcess, itemDecompressSnesTraces.CompressedSize);
        Interlocked.Increment(ref statsCompressedBlocksToProcess);
    }

    private void Stats_MarkCompleted(int bytesCompleted)
    {
        Interlocked.Add(ref statsBytesToProcess, -bytesCompleted);
        Interlocked.Decrement(ref statsCompressedBlocksToProcess);
    }

    private void ProcessWorkItemSnesTrace(BsnesImportStreamProcessor.WorkItemSnesTrace workItemSnesTrace, in TraceLogCaptureSettings captureSettings)
    {
        #if PROFILING
        var mainSpan = Markers.EnterSpan("BSNES ProcessWorkItem");
        #endif

        if (workItemSnesTrace.Buffer != null) // should always be non-null but
        {
            // this importer call is thread-safe, so we don't need to do our own locking
            // also, CaptureSettings will be passed by value and copied, so it's thread-safe.
            importer.ImportTraceLogLineBinary(workItemSnesTrace.Buffer, workItemSnesTrace.AbridgedFormat, captureSettings);
        }

        #if PROFILING
        mainSpan.Leave();
        #endif
    }

    public void SignalToStop()
    {
        streamProcessor.CancelToken.Cancel();
        taskManager.StartFinishing();
    }

    public (BsnesTraceLogImporter.Stats stats, int bytesToProcess) GetStats()
    {
        if (!Running)
            return (cachedStats, 0);

        // this is thread-safe and will make a copy for us.
        cachedStats = importer.CurrentStats;

        return (cachedStats, statsBytesToProcess);
    }
}

// mostly for testing, though we could use it to speed up importing too.
// public class BsnesTraceLogFileCapture : BsnesTraceLogCapture
// {
//     private readonly byte[] bytes;
//
//     public BsnesTraceLogFileCapture(string dataFile, ISnesData data) : base(data)
//     {
//         bytes = File.ReadAllBytes(dataFile);
//     }
//
//     protected override Stream GetInputStream()
//     {
//         return new MemoryStream(bytes);
//     }
// }

// // debug only. same as above, but, run it a bunch of times mostly for performance
// // testing and memory allocation reasons.
// public class BsnesTraceLogDebugBenchmarkFileCapture : BsnesTraceLogFileCapture
// {
//     private readonly int numTimes = 1;
//
//     // for benchmarking, these are called immediate before and after Main(), which represents the bulk of the CPU work.
//     // if optimizing, making these work faster is a good place to start.
//     public Action OnStart { get; set; }
//     public Action OnStop { get; set; }
//     
//     protected override void Main()
//     {
//         OnStart?.Invoke();
//         
//         for (var i = 0; i < numTimes; ++i)
//             base.Main();
//
//         OnStop?.Invoke();
//     }
//
//     public BsnesTraceLogDebugBenchmarkFileCapture(string dataFile, int numTimes) : base(dataFile)
//     {
//         this.numTimes = numTimes;
//     }
// }