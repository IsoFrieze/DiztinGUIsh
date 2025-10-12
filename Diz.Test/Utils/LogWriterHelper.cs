using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Diz.Core.export;
using Diz.Core.model.snes;
using Diz.LogWriter;
using Diz.LogWriter.util;
using FluentAssertions;
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
                return Label == other.Label && Instr == other.Instr && CommentPc == other.CommentPc && CommentRawBytes == other.CommentRawBytes && CommentIa == other.CommentIa && CommentActualText == other.CommentActualText;
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
                    hashCode = (hashCode * 397) ^ CommentPc.GetHashCode();
                    hashCode = (hashCode * 397) ^ CommentRawBytes.GetHashCode();
                    hashCode = (hashCode * 397) ^ CommentIa.GetHashCode();
                    hashCode = (hashCode * 397) ^ CommentActualText.GetHashCode();
                    return hashCode;
                }
            }

            public string Label;
            public string Instr;

            public string CommentPc;
            public string CommentRawBytes;
            public string CommentIa;

            public string CommentActualText;
        }

        private static ParsedOutput ParseLine(string line)
        {
            if (line == "")
                return null;

            var output = new ParsedOutput();

            var split = line.Split([';'], 2,options:StringSplitOptions.None);
            var main = split[0].Trim(); 
            var comment = split[1].Trim();

            // parse the stuff in the comment (it may or may not be there)
            var csplit = comment.Split(['|'], 3, options: StringSplitOptions.None);
            if (csplit.Length <= 3)
            {
                output.CommentActualText = comment;
            }
            else
            {
                output.CommentPc = csplit[0].Trim();
                output.CommentRawBytes = csplit[1].Trim();

                var iasplit = csplit[2].Split([';'], 2, options: StringSplitOptions.None);
                output.CommentIa = iasplit[0].Trim();
                output.CommentActualText = iasplit[1].Trim();
            }

            var msplit = main.Split([':'], 2, options: StringSplitOptions.None);
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
            testOutputHelper?.WriteLine(result.AssemblyOutputStr);
            
            AssertGoodOutput(result);
            
            // parse the output so we can better pinpoint where errors are
            var expectedOut = ParseAll(expectedRaw);
            var actualOut = ParseAll(result.AssemblyOutputStr);
            AssertAssemblyOutputEqual(expectedOut, actualOut);
            
            // now that the parsed version passed, compare the raw strings
            // if you hit this and not the above section, your whitespace or newline [CRLF vs LF] might be off.
            result.AssemblyOutputStr.Should().Be(expectedRaw);
        }

        public static LogCreatorOutput.OutputResult ExportAssembly(Data inputRom, Action<LogCreator> postInitHook = null)
        {
            var logCreator = new LogCreator
            {
                Data = new LogCreatorByteSource(inputRom),
                Settings = new LogWriterSettings {
                    OutputToString = true,
                    Structure = LogWriterSettings.FormatStructure.SingleFile,
                    SuppressSingleFileModeDisabledError = true, // hack for now
                }
            };
            
            postInitHook?.Invoke(logCreator);

            return logCreator.CreateLog();
        }

        [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local")]
        private static void AssertGoodOutput(LogCreatorOutput.OutputResult result)
        {
            Assert.True(result.LogCreator != null);
            Assert.True(result.AssemblyOutputStr != null);
            Assert.True(result.ErrorCount == 0);
        }

        private static void AssertAssemblyOutputEqual(IReadOnlyList<ParsedOutput> expectedOut, IReadOnlyList<ParsedOutput> actualOut)
        {
            TestUtil.AssertCollectionEqual(expectedOut, actualOut);
        }
    }
}