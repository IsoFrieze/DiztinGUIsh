using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Import;

public class LabelImporterCsv : LabelImporter
{
    public static bool IsFileCompatible(string importFilename) => 
        importFilename.ToLower().EndsWith(".csv");

    protected override (IAnnotationLabel label, string labelAddress)? TryParseLabelFromLine(string line)
    {
        // TODO: replace with something better. this is kind of a risky/fragile way to parse CSV lines.
        // it won't deal with weirdness in the comments, quotes, etc.
        Util.SplitOnFirstComma(line, out var labelAddress, out var remainder);
        Util.SplitOnFirstComma(remainder, out var labelName, out var labelComment);
        var label = new Label
        {
            Name = labelName.Trim(),
            Comment = labelComment
        };
        return (label, labelAddress);
    }
}