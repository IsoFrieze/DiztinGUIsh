namespace Diz.Cpu._65816;

public class CpuUtils
{
    public class OperandOverride
    {
        // completely override the operand text with this user-specified text
        // this is the complete wild west: no checks will be done, etc.
        public string TextToOverride { get; set; } = "";
        
        // if true, never print a label (always print the raw hex)
        // useful for things like PEA or PER instructions which may falsely grab labels
        public bool ForceOnlyShowRawHex { get; set; }
        
        // if true, then this particular label WONT create a temporary label
        // at its original offset (useful for things like PTR_ or DATA_ destinations where
        // the label value here is used for accessing memory that's really not related to it.
        // for instance, if a game is doing "LDA.L $C00000, X", and accesisng lots of locations using 
        // different values in X, then, we might not want to stick a "DATA_" label at $C00000
        public bool DontGenerateTemporaryLabelAtDestination { get; set; }

        public enum FormatOverride
        {
            None,
            AsDecimal,
            // add more as desired
        }

        public enum IncSrcOverride
        {
            None,
            IncSrcStart,
            IncSrcEnd,
        }

        public FormatOverride ConstantFormatOverride { get; set; } = FormatOverride.None;
        public IncSrcOverride IncludeSrc { get; set; } = IncSrcOverride.None;
    }
    
    /// <summary>
    /// Parse special override directives that may be present in a comment
    /// These can override generated labels, or force no labels to be generated, among other things.
    /// Typically, special directives start with "!!" and contain some commands 
    /// </summary>
    /// <param name="inputText"></param>
    /// <returns></returns>
    public static OperandOverride? ParseCommentSpecialDirective(string? inputText)
    {
        // TODO: allow multiple directives in a line separated by delimiter
        
        if (string.IsNullOrEmpty(inputText) || !inputText.StartsWith("!!"))
            return null;
        
        // filter anything after a semicolon, which we'll treat like a comment and ignore. trim leftover whitespace
        var semicolonIndex = inputText.IndexOf(';');
        if (semicolonIndex >= 0) {
            inputText = inputText[..semicolonIndex].Trim();
        }
        if (string.IsNullOrEmpty(inputText))
            return null;
        
        // Remove the "!!" prefix and continue processing
        var cmd = inputText[2..];

        // option 1: override real label or correct hex with whatever our text is
        // (does ZERO checking to ensure it's valid, it's all up to the user now)
        if (cmd.StartsWith("o "))
        {
            var textToOverride = cmd[2..]; // skip "o "
            var directive = new OperandOverride {
                TextToOverride = textToOverride
            };
            
            // normally, we'd be done right here, but, let's see if this seems to contain an expression. if so,
            // we'll also disable this label from being able to create a temporary destination label (like DATA_xxx or PTR_xxx).
            // (other uses of the same address might still create that destination temp label though)
            // if this causes any issues, feel free to turn it off.
            // feel free to add more expression checks here:
            if (textToOverride.Contains('!') || textToOverride.Contains('+') || textToOverride.Contains('-'))
                directive.DontGenerateTemporaryLabelAtDestination = true;
            
            return directive;
        }

        // option 2: force output to never allow a label on this line 
        if (cmd.StartsWith('n'))
        {
            return new OperandOverride {
                ForceOnlyShowRawHex = true
            };
        }
        
        // option3: create an incsrc directive from this, as though it's a Region.
        // must have a "is" and a "ie" tag (include start, include end) and a label on the start, for this to work
        // this is just a lazier way to define incsrc directives (instead of having to create a Region and use the "export as new file" option)
        if (cmd.StartsWith('i') && cmd.Length == 2)
        {
            return cmd[1] switch
            {
                's' => new OperandOverride { IncludeSrc = OperandOverride.IncSrcOverride.IncSrcStart },
                'e' => new OperandOverride { IncludeSrc = OperandOverride.IncSrcOverride.IncSrcEnd },
                _ => null
            };
        }
        
        // option 3: (if applicable) force this constant to show up as Decimal instead of Hex.
        // like "!!fd" = format as decimal
        if (cmd.StartsWith('f') && cmd.Length == 2)
        {
            if (cmd[1] == 'd')
            {
                return new OperandOverride {
                    ConstantFormatOverride = OperandOverride.FormatOverride.AsDecimal
                };   
            }
        }

        return null;
    }
}