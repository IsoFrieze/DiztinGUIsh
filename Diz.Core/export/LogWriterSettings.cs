#nullable enable

using System.IO;
using System.Xml.Serialization;
using Diz.Core.util;

/*
 * TODO:
 * Couple things for ongoing refactors:
 * 1) This class should ideally live with the Diz.LogWriter project (not Diz.Core)
 * 2) Probably use dependency injection starting with this system to register settings providers in Diz like this one?
 */

namespace Diz.Core.export;

public record LogWriterSettings
{
    // path to output file or folder
    public const string DefaultStr = "%label:-22% %code:37%;%pc%|%bytes%|%ia%; %comment%";
        
    public enum FormatUnlabeled
    {
        ShowAll = 0,
        ShowInPoints = 1, // TODO Add Show In Points with +/- labels
        ShowNone = 2
    }

    public enum FormatStructure
    {
        SingleFile = 0,
        OneBankPerFile = 1
    }

    public string Format { get; init; } = DefaultStr;
    public int DataPerLine { get; init; } = 8;
    public FormatUnlabeled Unlabeled { get; init; } = FormatUnlabeled.ShowInPoints;
    public FormatStructure Structure { get; init; } = FormatStructure.OneBankPerFile;
    public bool IncludeUnusedLabels  { get; init; }
    public bool PrintLabelSpecificComments { get; init; }
    public bool OutputExtraWhitespace  { get; init; } = true;
        
    /// <summary>
    /// specify an override for the # of bytes to assemble. default is to visit every byte in the entire ROM 
    /// </summary>
    public int RomSizeOverride { get; init; } = -1;

    /// <summary>
    /// The (usually absolute) base path to the project directory, if any.
    /// Don't save this with the project XML.
    /// </summary>
    [XmlIgnore]
    public string BaseOutputPath { get; init; } = "";
        
    /// <summary>
    /// Relative path to add on after the base path.
    /// </summary>
    public string FileOrFolderOutPath { get; init; } = "export\\";

    public bool OutputToString { get; init; }
    public string ErrorFilename { get; init; } = "errors.txt";

    public LogWriterSettings WithPathRelativeTo(string newFileNameAndPath, string pathToMakeRelativeTo) =>
        this with
        {
            FileOrFolderOutPath = Util.TryGetRelativePath(newFileNameAndPath, pathToMakeRelativeTo),
            BaseOutputPath = pathToMakeRelativeTo,
        };

    public string BuildFullOutputPath()
    {
        var relativeOutPath = FileOrFolderOutPath;

        if (Structure == FormatStructure.OneBankPerFile)
            relativeOutPath += "\\"; // force it to treat it as a path.

        var relativeFolderPath = Path.GetDirectoryName(relativeOutPath) ?? "";

        return Path.Combine(BaseOutputPath, relativeFolderPath);
    }
}

public static class LogWriterSettingsExtensions
{
    public static LogWriterSettings GetDefaultsIfInvalid(this LogWriterSettings @this) =>
        @this.IsValid() ? @this : new LogWriterSettings();

    public static string? Validate(this LogWriterSettings @this)
    {
        var results = new LogWriterSettingsValidator().Validate(@this);
        return !results.IsValid ? results.ToString() : null;
    }

    public static bool IsValid(this LogWriterSettings @this) => 
        @this.Validate() != null;
}