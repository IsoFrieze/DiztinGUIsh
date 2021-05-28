using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Diz.Test.Tests.PerformanceTests;
using Xunit.Abstractions;

namespace Diz.Test.Utils
{
    public static class XunitBenchmark
    {
        public static void Run<T>(ITestOutputHelper xUnitOutput, bool debugOk=false)
        {
            var logger = new XOutLogger(xUnitOutput);
            var config = ManualConfig.Create(DefaultConfig.Instance);
            config.AddLogger(logger);

            // don't use this for capturing performance info. just for debugging harnesses.
            // always run benchmarks in release mode.
            if (debugOk)
                config.Options &= ConfigOptions.DisableOptimizationsValidator;

            BenchmarkRunner.Run<T>(config);
        }
    }
}