using System.IO;
using Diz.Core.util;

namespace Diz.Core.export
{
    public struct LogWriterSettings
    {
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
        
        // struct because we want to make a bunch of copies of this struct.
        // The plumbing could use a pass of something like 'ref readonly' because:
        // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref#reference-return-values

        public string Format;
        public int DataPerLine;
        public LogWriterSettings.FormatUnlabeled Unlabeled;
        public LogWriterSettings.FormatStructure Structure;
        public bool IncludeUnusedLabels;
        public bool PrintLabelSpecificComments;
        public bool OutputExtraWhitespace;

        private bool wasInitialized;
        public int RomSizeOverride; // specify an override for the # of bytes to assemble. default is the entire ROM

        private string fileOrFolderOutPath;
        public string FileOrFolderOutPath
        {
            get => fileOrFolderOutPath;
            set => fileOrFolderOutPath = value;
        }
        
        public void SetFileOrFolderOutputPathRelativeToDir(string fileOrFolderPath, string projectProjectDirectory)
        {
            FileOrFolderOutPath = Util.TryGetRelativePath(fileOrFolderPath, projectProjectDirectory);
        }

        public bool OutputToString;
        public string ErrorFilename;
        
        public const string DefaultStr = "%label:-22% %code:37%;%pc%|%bytes%|%ia%; %comment%";

        public void SetDefaults()
        {
            Format = DefaultStr;
            DataPerLine = 8;
            Unlabeled = LogWriterSettings.FormatUnlabeled.ShowInPoints;
            Structure = LogWriterSettings.FormatStructure.OneBankPerFile;
            IncludeUnusedLabels = false;
            PrintLabelSpecificComments = false;
            FileOrFolderOutPath = ""; // path to output file or folder
            wasInitialized = true;
            RomSizeOverride = -1;
            ErrorFilename = "errors.txt";
            OutputExtraWhitespace = true;
        }

        // return null if no error, or message if there is
        public string Validate()
        {
            // for now, just make sure it was initialized somewhere by someone
            if (!wasInitialized)
                return "Not initialized";

            // TODO: add more validation.

            if (OutputToString)
            {
                if (Structure == LogWriterSettings.FormatStructure.OneBankPerFile)
                    return "Can't use one-bank-per-file output with string output";

                if (FileOrFolderOutPath != "")
                    return "Can't use one-bank-per-file output with file output";
            }
            else
            {
                if (FileOrFolderOutPath == "")
                    return "No file path set";

                if (!Directory.Exists(Path.GetDirectoryName(FileOrFolderOutPath)))
                    return "File or folder output directory doesn't exist";

                if (Structure == LogWriterSettings.FormatStructure.SingleFile)
                {
                    // don't check for existence, just that what we have appears to be a filename and
                    // not a directory.
                    if (Path.GetFileName(FileOrFolderOutPath) == string.Empty)
                        return "Output path doesn't appear to be a valid file selection";
                }
            }

            return null;
        }
    }

}
