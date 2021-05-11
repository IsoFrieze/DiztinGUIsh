using System;
using System.Collections.Generic;
using System.IO;

namespace Diz.Core.export
{
    public class LogCreatorLineFormatter
    {
        public record ColumnFormat
        {
            public string Value { get; init; }
            public int? LengthOverride { get; init; }
            public bool IsLiteral { get; init; }
            public bool IgnoreOffset { get; init; }

            public int? SanitizeOffset(int offset)
            {
                return IgnoreOffset || offset == -1 ? null : offset;
            }
        }

        public string FormatString { get; }
        public List<ColumnFormat> ColumnFormats { get; private set; }

        private readonly IReadOnlyDictionary<string, AssemblyPartialLineGenerator> generators;

        public LogCreatorLineFormatter(string lineFormatStr, IReadOnlyDictionary<string, AssemblyPartialLineGenerator> generators)
        {
            this.generators = generators;
            FormatString = lineFormatStr;
            
            Parse();
        }
        
        public LogCreatorLineFormatter(string lineFormatStr) : this(lineFormatStr, AssemblyGeneratorRegistration.Create()) {}
        
        // every line printed in a .asm file is done so by variable substitution according to a format string.
        //
        // example:
        // in a format string like this:
        // "%label:-22% %code:37%;%pc%|%bytes%|%ia%; %comment%";
        //
        // you might get output like this:
        //
        // CODE_808000: LDA.W Test_Data,X                    ;808000|BD5B80  |80805B;
        //
        // GetParameter() takes a ROM offset and the name of a "parameter" i.e. one of the labels in that format string
        // like "label", "code", "pc", "bytes", etc.  There are also special params that start with a % sign, like
        // "%empty", "%map", "%bankcross" etc.
        //
        // 
        // It will look for a function in LogCreator tagged with an AssemblerHandler attribute that matches the 
        // parameter passed in.
        private void Parse()
        {
            var output = new List<ColumnFormat>();
            
            var split = FormatString.Split('%');
            
            if (split.Length % 2 == 0)
                throw new InvalidDataException("Format string has a non-even amount of % signs");
            
            for (var i = 0; i < split.Length; i++)
            {
                var isLiteral = i % 2 == 0; 
                ParseOneItem(isLiteral, output, split[i]);
            }

            ColumnFormats = output;
        }

        private void ParseOneItem(bool isLiteral, ICollection<ColumnFormat> output, string token)
        {
            try
            {
                var newItem = isLiteral
                    ? ParseStringLiteral(token)
                    : ParseFormatItem(token);
                
                if (newItem != null)
                    output.Add(newItem);
            }
            catch (Exception ex)
            {
                ex.Data.Add("LineFormatterToken", token);
                throw;
            }
        }
        
        private ColumnFormat ParseFormatItem(string token)
        {
            var (itemValue, overrideLenStr) = ParseItemValue(token);
            var lengthOverride = GetLengthOverride(overrideLenStr);
            
            EnsureValidGeneratorExistsFor(itemValue);

            return new ColumnFormat
            {
                Value = itemValue,
                LengthOverride = lengthOverride,
            };
        }

        private static (string val, string overrideLen) ParseItemValue(string token)
        {
            var indexOfColon = token.IndexOf(':');
            
            // default, length comes from the attribute, generator not involved
            // example: "%label%"
            if (indexOfColon < 0)
                return (token, null);

            // override, length comes from the format string
            // example: for token "%label:-22%", length would be "-22"
            return (
                token.Substring(0, indexOfColon), 
                token.Substring(indexOfColon + 1)
                );
        }

        private static int? GetLengthOverride(string overrideLenStr)
        {
            if (overrideLenStr == null) 
                return null;

            if (!int.TryParse(overrideLenStr, out var lenOverride))
                throw new InvalidDataException($"Invalid length");

            return lenOverride;
        }

        private void EnsureValidGeneratorExistsFor(string itemValue)
        {
            var validGenerator = generators.TryGetValue(itemValue, out _);
            if (!validGenerator)
                throw new InvalidDataException($"Can't find assembly generator");
        }

        private static ColumnFormat ParseStringLiteral(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;
            
            return new()
            {
                Value = token,
                IsLiteral = true
            };
        }
        
        public static bool Validate(string formatStr)
        {
            try
            {
                var unused = new LogCreatorLineFormatter(formatStr);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}