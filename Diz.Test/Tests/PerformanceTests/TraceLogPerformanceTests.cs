﻿using System;
using System.Diagnostics;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Loggers;
using Diz.Core.model.snes;
using Diz.Cpu._65816;
using Diz.Import.bsnes.tracelog;
using Diz.Test.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test
{
    public sealed class XOutLogger : ILogger
    {
        private readonly ITestOutputHelper _helper;

        public XOutLogger(ITestOutputHelper helper)
        {
            _helper = helper;
        }

        public void Write(LogKind logKind, string text)
        {
            _helper.WriteLine(text); // not quite correct but works.
        }

        public void WriteLine()
        {
            _helper.WriteLine("\n");
        }

        public void WriteLine(LogKind logKind, string text)
        {
            _helper.WriteLine(text);
        }

        public void Flush()
        {
        }

        public string Id { get; } = "xunitLogger";
        public int Priority { get; }
    }

    public class TraceLogPerformanceTests
    {
        private readonly ITestOutputHelper output;

        public TraceLogPerformanceTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private void RunPrintTiming(Action item)
        {
            var s = Stopwatch.StartNew();
            s.Start();
                
            item.Invoke();
                
            s.Stop();
            output.WriteLine($"runtime: {s.ElapsedMilliseconds:N0}ms");
        }

        public class ImportTraceLogStreamTestHarness
        {
            const string datafile = "..\\..\\testdata\\ct-binary-tracelog7.758s-60fps-locked.bin";
            readonly Data Data = new EmptyRom();
            private readonly BsnesTraceLogDebugBenchmarkFileCapture capturing;
            private readonly Stopwatch stopWatch = new();
            private readonly ITestOutputHelper output;

            public ImportTraceLogStreamTestHarness(ITestOutputHelper output)
            {
                this.output = output;

                var cwd = Directory.GetCurrentDirectory();
                var fullPath = Path.Combine(cwd, datafile);

                capturing = new BsnesTraceLogDebugBenchmarkFileCapture(fullPath, 1)
                {
                    OnStart = () => { stopWatch.Reset(); stopWatch.Start(); },
                    OnStop = () =>
                    {
                        stopWatch.Stop();
                        this.output.WriteLine($"runtime: {stopWatch.ElapsedMilliseconds:N0}ms");
                    }
                };
            }

            [Benchmark]
            public void Run()
            {
                capturing.Run(Data.GetSnesApi());
            }
        }

        [Fact(Skip="skipping b/c external dependency")]
        public void TestTraceLogPerformance()
        {
            var test = new ImportTraceLogStreamTestHarness(output);
            test.Run();
            // XunitBenchmark.Run<ImportTraceLogStream>(output);
        }
    }
}