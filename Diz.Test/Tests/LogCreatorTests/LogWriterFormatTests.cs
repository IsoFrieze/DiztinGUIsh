// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Diz.Core.export;
using Diz.Test.Utils;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.LogCreatorTests
{
    public static class LogWriterFormatTests
    {
        public class LogFormatStrTestHarness
        {
            public List<LogCreatorLineFormatter.ColumnFormat> ExpectedOutput { get; init; }
            public string FormatStr { get; init; }
            public Dictionary<string, AssemblyPartialLineGenerator> Generators
            {
                // default: use the built-in system generators unless we explicitly override
                get => generators ??= AssemblyGeneratorRegistration.Create();
                
                init
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
                    ExpectedOutput = new List<LogCreatorLineFormatter.ColumnFormat>
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
            TestUtil.AssertCollectionEqual(harness.ExpectedOutput, formatter.ColumnFormats);
        }

        public static TheoryData<string> BadUnevenPercentageSignStrings => new()
        {
            "%",
            "%asdf",
            "%asdf%%",
        };
            
        public static TheoryData<string> BadLengthOverride => new() 
        {
            "%asdf:%",
            "%asdf:G%",
            "%asdf:000A%",
            "%asdf:--12%",
        };
        
        
        [Theory]
        [MemberData(nameof(BadLengthOverride))]
        public static void TestLogWriterLengthFormatStrInvalid(string invalidParseStr)
        {
            var x = GetLogCreatorAction(invalidParseStr);
            x.Should().Throw<InvalidDataException>().WithMessage("*length*");
        }
        
        [Theory]
        [MemberData(nameof(BadUnevenPercentageSignStrings))]
        public static void TestLogWriterFormatStrInvalid(string invalidParseStr)
        {

            var x = GetLogCreatorAction(invalidParseStr);
            x.Should().Throw<InvalidDataException>().WithMessage("*non-even*");
        }

        private static Action GetLogCreatorAction(string invalidParseStr)
        {
            // ReSharper disable once ObjectCreationAsStatement
            // ReSharper disable once HeapView.ObjectAllocation.Evident
            // ReSharper disable once CA1806
            return () => new LogCreatorLineFormatter(invalidParseStr, null);
        }
    }
}