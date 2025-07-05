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
        public bool ForceNoLabel { get; set; }
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
            return new OperandOverride {
                TextToOverride = textToOverride
            };
        }

        // option 2: 
        if (cmd.StartsWith('n'))
        {
            return new OperandOverride {
                ForceNoLabel = true
            };
        }

        return null;
    }
}