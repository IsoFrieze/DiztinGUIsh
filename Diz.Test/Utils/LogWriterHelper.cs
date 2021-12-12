﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Diz.Core.export;
using Diz.Core.model.snes;
using Diz.LogWriter;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test.Utils
{
    public static class LogWriterHelper
    {
        public class ParsedOutput
        {
            private bool Equals(ParsedOutput other)
            {
                return Label == other.Label && Instr == other.Instr && Pc == other.Pc && RawBytes == other.RawBytes && Ia == other.Ia && RealComment == other.RealComment;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ParsedOutput) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Label.GetHashCode();
                    hashCode = (hashCode * 397) ^ Instr.GetHashCode();
                    hashCode = (hashCode * 397) ^ Pc.GetHashCode();
                    hashCode = (hashCode * 397) ^ RawBytes.GetHashCode();
                    hashCode = (hashCode * 397) ^ Ia.GetHashCode();
                    hashCode = (hashCode * 397) ^ RealComment.GetHashCode();
                    return hashCode;
                }
            }

            public string Label;
            public string Instr;

            public string Pc;
            public string RawBytes;
            public string Ia;

            public string RealComment;
        }

        private static ParsedOutput ParseLine(string line)
        {
            if (line == "")
                return null;

            var output = new ParsedOutput();

            var split = line.Split(new[] {';'}, 2,options:StringSplitOptions.None);
            var main = split[0].Trim(); 
            var comment = split[1].Trim();

            var csplit = comment.Split(new[] { '|' }, 3, options: StringSplitOptions.None);
            output.Pc = csplit[0].Trim();
            output.RawBytes = csplit[1].Trim();

            var iasplit = csplit[2].Split(new[] { ';' }, 2, options: StringSplitOptions.None);
            output.Ia = iasplit[0].Trim();
            output.RealComment = iasplit[1].Trim();

            var msplit = main.Split(new[] { ':' }, 2, options: StringSplitOptions.None);
            var m1 = msplit[0].Trim();
            var m2  = msplit.Length > 1 ? msplit[1].Trim() : "";

            if (m2 != "")
            {
                output.Label = m1;
                output.Instr = m2;
            }
            else
            {
                output.Label = "";
                output.Instr = m1;
            }

            return output;
        }

        private static List<ParsedOutput> ParseAll(string lines) =>
            lines.Split(new[] {'\n'})
                .Select(line => ParseLine(line.Trim()))
                .ToList();

        public static void AssertAssemblyOutputEquals(string expectedRaw, LogCreatorOutput.OutputResult result, ITestOutputHelper testOutputHelper = null)
        {
            testOutputHelper?.WriteLine("** EXPECTED **");
            testOutputHelper?.WriteLine(expectedRaw);
            
            testOutputHelper?.WriteLine("** ACTUAL **");
            testOutputHelper?.WriteLine(result.OutputStr);
            
            AssertGoodOutput(result);
            
            // parse the output so we can better pinpoint where errors are
            var expectedOut = ParseAll(expectedRaw);
            var actualOut = ParseAll(result.OutputStr);
            AssertAssemblyOutputEqual(expectedOut, actualOut);
            
            // now that the parsed version passed, compare the raw strings
            // if you hit this and not the above section, your whitespace might be off.
            Assert.Equal(expectedRaw, result.OutputStr);
        }

        public static LogCreatorOutput.OutputResult ExportAssembly(Data inputRom, Action<LogCreator> postInitHook = null)
        {
            var logCreator = new LogCreator
            {
                Data = inputRom,
                Settings = new LogWriterSettings {
                    OutputToString = true,
                    Structure = LogWriterSettings.FormatStructure.SingleFile
                }
            };
            
            postInitHook?.Invoke(logCreator);

            return logCreator.CreateLog();
        }

        [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local")]
        private static void AssertGoodOutput(LogCreatorOutput.OutputResult result)
        {
            Assert.True(result.LogCreator != null);
            Assert.True(result.OutputStr != null);
            Assert.True(result.ErrorCount == 0);
        }

        private static void AssertAssemblyOutputEqual(IReadOnlyList<ParsedOutput> expectedOut, IReadOnlyList<ParsedOutput> actualOut)
        {
            TestUtil.AssertCollectionEqual(expectedOut, actualOut);
        }
    }
}