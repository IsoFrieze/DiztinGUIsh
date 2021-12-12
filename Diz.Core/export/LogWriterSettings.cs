#nullable enable

using Diz.Core.util;

/*
 * TODO:
 * Couple things for ongoing refactors:
 * 1) This class should live with the Diz.LogWriter project (not Diz.Core)
 * 2) It should inherit some kind of IDocRootItem class and be saved alongside Diz.Project
 * 3) Diz.Core shouldn't have to know about anything in here or anything about assembly export/generation
 * 4) Probably use dependency injection starting with this system to register settings providers in Diz like this one
 */

namespace Diz.Core.export
{
    public record LogWriterSettings
    {
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
        
        public string Format { get; init; }
        public int DataPerLine { get; init; }
        public FormatUnlabeled Unlabeled { get; init; }
        public FormatStructure Structure { get; init; }
        public bool IncludeUnusedLabels  { get; init; }
        public bool PrintLabelSpecificComments { get; init; }
        public bool OutputExtraWhitespace  { get; init; }

        // specify an override for the # of bytes to assemble. default is the entire ROM
        public int RomSizeOverride { get; init; }
        public string FileOrFolderOutPath { get; init; }

        public bool OutputToString { get; init; }
        public string ErrorFilename { get; init; }
        
        public LogWriterSettings WithPathRelativeTo(string newFileNameAndPath, string pathToMakeRelativeTo) =>
            this with
            {
                FileOrFolderOutPath = Util.TryGetRelativePath(newFileNameAndPath, pathToMakeRelativeTo)
            };

        public LogWriterSettings()
        {
            Format = DefaultStr;
            DataPerLine = 8;
            Unlabeled = FormatUnlabeled.ShowInPoints;
            Structure = FormatStructure.OneBankPerFile;
            IncludeUnusedLabels = false;
            PrintLabelSpecificComments = false;
            RomSizeOverride = -1;
            ErrorFilename = "errors.txt";
            OutputExtraWhitespace = true;
            FileOrFolderOutPath = ""; // path to output file or folder
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
}
