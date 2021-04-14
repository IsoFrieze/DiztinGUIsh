using System;
using System.Collections.Generic;
using System.IO;

namespace Diz.Core.export
{
    public class LogCreatorLineFormatter
    {
        public class FormatItem
        {
            public string Value;
            public int? LengthOverride;
            public bool IsLiteral;
            
            protected bool Equals(FormatItem other)
            {
                return Value == other.Value && LengthOverride == other.LengthOverride && IsLiteral == other.IsLiteral;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((FormatItem) obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Value, LengthOverride, IsLiteral);
            }
        }

        public string FormatString { get; }
        public List<FormatItem> ParsedFormat { get; private set; }

        private readonly IReadOnlyDictionary<string, AssemblyPartialLineGenerator> generators;

        public LogCreatorLineFormatter(string lineFormatStr, IReadOnlyDictionary<string, AssemblyPartialLineGenerator> generators)
        {
            this.generators = generators;
            FormatString = lineFormatStr;
            
            Parse();
        }
        
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
            var output = new List<FormatItem>();
            
            var split = FormatString.Split('%');
            
            if (split.Length % 2 == 0)
                throw new InvalidDataException("Format string has a non-even amount of % signs");
            
            for (var i = 0; i < split.Length; i++)
            {
                var isLiteral = i % 2 == 0; 
                ParseOneItem(isLiteral, output, split[i]);
            }

            ParsedFormat = output;
        }

        private void ParseOneItem(bool isLiteral, ICollection<FormatItem> output, string token)
        {
            var newItem = isLiteral 
                ? ParseStringLiteral(token) 
                : ParseFormatItem(token);
            
            if (newItem != null)
                output.Add(newItem);
        }
        
        private FormatItem ParseFormatItem(string token)
        {
            var item = new FormatItem();

            string overrideLenStr = null;
            var indexColon = token.IndexOf(':');
            if (indexColon < 0)
            {
                // default, length comes from the attribute, generator not involved
                // example: "%label%"
                item.Value = token;
            }
            else
            {
                // override, length comes from the format string
                // example: for token "%label:-22%", length would be "-22"
                item.Value = token.Substring(0, indexColon);
                overrideLenStr = token.Substring(indexColon + 1);
            }
            
            var validGenerator = generators.TryGetValue(item.Value, out var generator);
            if (!validGenerator)
                throw new InvalidDataException($"Can't find handler for item '{item.Value}'");

            if (overrideLenStr != null)
            {
                if (!int.TryParse(overrideLenStr, out var lengthOverride))
                    throw new InvalidDataException($"Invalid length specified for '{item.Value}'");

                item.LengthOverride = lengthOverride;
            }

            return item;
        }

        private static FormatItem ParseStringLiteral(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;
            
            return new()
            {
                Value = token,
                IsLiteral = true
            };
        }
    }
}