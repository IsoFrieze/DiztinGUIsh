using Diz.Core.Interfaces;
using Diz.Core.model;

namespace Diz.Import.bsnes;

// there's a few different flavors of .sym.cpu files.
// one is here: https://github.com/BenjaminSchulte/fma-snes65816/blob/master/docs/symbols.adoc
// another is from the BSNES+ debugger, which is slightly different.  try and support both here if we can, or, split out the parser if needed.

public class BsnesSymbolLabelImporter : LabelImporter
{
    private string currentBsnesSection = "";

    public override Dictionary<int, IAnnotationLabel> ReadLabelsFromFile(string importFilename)
    {
        currentBsnesSection = "";
        return base.ReadLabelsFromFile(importFilename);
    }

    public static bool IsFileCompatible(string importFilename)
    {
        // Coming in from BSNES symbol map if it begins with the header
        return importFilename.ToLower().EndsWith(".cpu.sym");
        
        // here's another way to check if the file contents match.
        // this signature can be present (but isn't always) present in some BSNES versions (it's not in BSNES+)
        // the above filename extension check is probably sufficient for all of it though
        // lines.Length > 0 && lines[0].StartsWith("#SNES65816");
    }

    protected override (IAnnotationLabel label, string labelAddress)? TryParseLabelFromLine(string line)
    {
        if (ShouldSkipLineBecauseCommentOrWhitespace(line)) 
            return null;

        // did we enter a new section? if so, note it, and move to next line
        if (TryParseBsnesSection(line))
            return null;

        switch (currentBsnesSection)
        {
            case "[LABELS]":
            case "[SYMBOL]":
            {
                var symbols = line.Trim().Split(' ');
                var labelAddress = ParseSnesAddress(symbols[0]);
                var label = new Label
                {
                    Name = symbols[1].Replace(".", "_") // Replace dots which are valid in BSNES
                };
                return (label, labelAddress);
            }
            case "[COMMENT]":
            {
                var comments = line.Trim().Split(' ', 2);
                var labelAddress = ParseSnesAddress(comments[0]);
                var label = new Label
                {
                    Comment = comments[1].Replace("\"", "") // Remove quotes
                };
                return (label, labelAddress);
            }
        }

        return null;
    }

    private bool TryParseBsnesSection(string line)
    {
        // BSNES symbol files are multiple INI sections like "[symbol]"
        // we only care about a few of them for Diztinguish
        // if we hit a section header, consume it, keep going
        if (!line.StartsWith('[') || !line.EndsWith(']'))
            return false;

        currentBsnesSection = line.ToUpper();
        return true;
    }

    private static string ParseSnesAddress(string symbols) => 
        symbols.Replace(":", "").ToUpper();

    private static bool ShouldSkipLineBecauseCommentOrWhitespace(string line) => 
        line.Trim().Length == 0 || line.StartsWith('#') || line.StartsWith(';');
}