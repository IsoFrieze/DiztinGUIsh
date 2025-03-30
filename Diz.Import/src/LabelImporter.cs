using System.Globalization;
using System.Text.RegularExpressions;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Import.bsnes;

namespace Diz.Import;

public abstract class LabelImporter
{
    // after import, if there was an error, this will be the line# of what it was.
    // if -1, we parsed the entire file.
    public int LastErrorLineNumber { get; private set; } = -1;
    
    // we don't modify labels in the open project directly, instead we read them
    // into here and only return this on success.
    private readonly Dictionary<int, IAnnotationLabel> newLabels = new(); 
    
    public virtual Dictionary<int, IAnnotationLabel> ReadLabelsFromFile(string importFilename)
    {
        newLabels.Clear();
        LastErrorLineNumber = 0;
        
        var lineIndex = 0;
        foreach (var line in Util.ReadLines(importFilename))
        {
            LastErrorLineNumber = lineIndex + 1;
            ParseLine(line);
            lineIndex++;
        }
        
        if (lineIndex == 0)
            throw new InvalidDataException("No lines in file, can't import.");
        
        LastErrorLineNumber = -1;
        return newLabels;
    }

    private void ParseLine(string line)
    {
        var labelFound = TryParseLabelFromLine(line);
        if (labelFound == null) 
            return;
        
        var (label, labelAddress) = labelFound.Value;
        TryImportLabel(label, labelAddress);
    }

    private void TryImportLabel(IAnnotationLabel label, string labelAddress)
    {
        var validLabelChars = new Regex(@"^([a-zA-Z0-9_\-+\.\-]*)$");
        if (!validLabelChars.Match(label.Name).Success)
            throw new InvalidDataException("invalid label name: " + label.Name);

        var address = int.Parse(labelAddress, NumberStyles.HexNumber, null);
        if (!newLabels.ContainsKey(address))
        {
            newLabels.Add(address, label);
        }
        else
        {
            // Update empty label properties instead of overwriting the entire object
            // if there are multiple definitions (like from BSNES or handmade CSV)
            var thisLabel = newLabels[address];

            if (thisLabel.Name.Length > 0)
                thisLabel.Name = label.Name;

            if (thisLabel.Comment.Length > 0)
                thisLabel.Comment = label.Comment;
        }
    }

    protected abstract (IAnnotationLabel label, string labelAddress)? TryParseLabelFromLine(string line);
}

public static class LabelImporterUtils
{
    // exception handling/line# stuff needs a little rework, messy.
    public static void ImportLabelsFromCsv(this ILabelProvider labelProvider, string importFilename, bool replaceAll, bool smartMerge, out int errLine)
    {
        // could probably do this part more elegantly
        errLine = 0;
        LabelImporter? importer = null;
        if (BsnesSymbolLabelImporter.IsFileCompatible(importFilename))
        {
            importer = new BsnesSymbolLabelImporter();
        }
        else if (LabelImporterCsv.IsFileCompatible(importFilename))
        {
            importer = new LabelImporterCsv();
        }

        if (importer == null)
        {
            throw new InvalidDataException($"No importer was found that can import a file named:\n'{importFilename}'");
        }

        var labelsFromFile = importer.ReadLabelsFromFile(importFilename);
        if (importer.LastErrorLineNumber != -1)
        {
            errLine = importer.LastErrorLineNumber;
            throw new InvalidDataException(
                $"Error importing file:\n'{importFilename}'\nNear line#: {importer.LastErrorLineNumber}");
        }

        if (replaceAll)
            labelProvider.DeleteAllLabels();
        
        labelProvider.AppendLabels(labelsFromFile, smartMerge: smartMerge);
    }
}