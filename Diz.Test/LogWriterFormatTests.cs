// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Diz.Core.export;
using Diz.Test.Utils;
using Sprache;
using Xunit;

namespace Diz.Test
{
    public static class LogWriterFormatTests
    {
        public class LogFormatStrTestHarness
        {
            public List<LogCreatorLineFormatter.FormatItem> ExpectedOutput { get; init; }
            public string FormatStr { get; init; }
            public Dictionary<string, AssemblyPartialLineGenerator> Generators
            {
                // default: use the built-in system generators unless we explicitly override
                get => generators ??= AssemblyGeneratorRegistration.Create();
                
                set
                {
                    Debug.Assert(generators == null);
                    generators = value;
                }
            }
            
            private Dictionary<string, AssemblyPartialLineGenerator> generators;
        }

        public class MockAsmGenerator : AssemblyPartialLineGenerator
        {

        }

        public static TheoryData<LogFormatStrTestHarness> LogWriterGenerators => 
            new() {
                new LogFormatStrTestHarness
                {
                    // input
                    FormatStr = "%test1%;%test2%|%test3:-20% LITERALTEXTHERE %test4:20%%test1:-333%",
                    Generators = new List<AssemblyPartialLineGenerator>
                    {
                        new MockAsmGenerator { Token = "test1", DefaultLength = 1 },
                        new MockAsmGenerator { Token = "test2", DefaultLength = 2 },
                        new MockAsmGenerator { Token = "test3", DefaultLength = 3 },
                        new MockAsmGenerator { Token = "test4", DefaultLength = 4 },
                    }.ToDictionary(generator => generator.Token),

                    // output
                    ExpectedOutput = new List<LogCreatorLineFormatter.FormatItem>
                    {
                        new() { Value = "test1" },
                        new() { Value = ";", IsLiteral = true }, 
                        new() { Value = "test2" },
                        new() { Value = "|", IsLiteral = true },
                        new() { Value = "test3", LengthOverride = -20 },
                        new() { Value = " LITERALTEXTHERE ", IsLiteral = true },
                        new() { Value = "test4", LengthOverride = 20 },
                        new() { Value = "test1", LengthOverride = -333 },
                    }
                }
            };

        [Theory]
        [MemberData(nameof(LogWriterGenerators))]
        public static void TestLogWriterFormatStr(LogFormatStrTestHarness harness)
        {
            var formatter = new LogCreatorLineFormatter(harness.FormatStr, harness.Generators);
            TestUtil.AssertCollectionEqual(harness.ExpectedOutput, formatter.ParsedFormat);
        }
    }
}