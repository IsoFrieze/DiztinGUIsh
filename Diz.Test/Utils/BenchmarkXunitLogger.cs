using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Xunit.Abstractions;

namespace Diz.Test.Utils
{
    public static class XunitBenchmark
    {
        public static void Run<T>(ITestOutputHelper xUnitOutput, bool debugOK=false)
        {
            var logger = new XOutLogger(xUnitOutput);
            var config = ManualConfig.Create(DefaultConfig.Instance);
            config.AddLogger(logger);

            // don't use this for capturing performance info. just for debugging harnesses.
            // always run benchmarks in release mode.
            if (debugOK)
                config.Options &= ConfigOptions.DisableOptimizationsValidator;

            BenchmarkRunner.Run<T>(config);
        }
    }
}