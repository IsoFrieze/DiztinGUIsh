using System.IO;

namespace Diz.Core.export
{
    public struct LogWriterSettings
    {
        // struct because we want to make a bunch of copies of this struct.
        // The plumbing could use a pass of something like 'ref readonly' because:
        // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref#reference-return-values

        public string format;
        public int dataPerLine;
        public LogCreator.FormatUnlabeled unlabeled;
        public LogCreator.FormatStructure structure;
        public bool includeUnusedLabels;
        public bool printLabelSpecificComments;

        public bool wasInitialized;
        public int romSizeOverride; // specify an override for the # of bytes to assemble. default is the entire ROM

        public string fileOrFolderOutPath;
        public bool outputToString;
        public string errorFilename;

        public void SetDefaults()
        {
            format = "%label:-22% %code:37%;%pc%|%bytes%|%ia%; %comment%";
            dataPerLine = 8;
            unlabeled = LogCreator.FormatUnlabeled.ShowInPoints;
            structure = LogCreator.FormatStructure.OneBankPerFile;
            includeUnusedLabels = false;
            printLabelSpecificComments = false;
            fileOrFolderOutPath = ""; // path to output file or folder
            wasInitialized = true;
            romSizeOverride = -1;
            errorFilename = "errors.txt";
        }

        // return null if no error, or message if there is
        public string Validate()
        {
            // for now, just make sure it was initialized somewhere by someone
            if (!wasInitialized)
                return "Not initialized";

            // TODO: add more validation.

            if (outputToString)
            {
                if (structure == LogCreator.FormatStructure.OneBankPerFile)
                    return "Can't use one-bank-per-file output with string output";

                if (fileOrFolderOutPath != "")
                    return "Can't use one-bank-per-file output with file output";
            }
            else
            {
                if (fileOrFolderOutPath == "")
                    return "No file path set";

                if (!Directory.Exists(Path.GetDirectoryName(fileOrFolderOutPath)))
                    return "File or folder output directory doesn't exist";

                if (structure == LogCreator.FormatStructure.SingleFile)
                {
                    // don't check for existence, just that what we have appears to be a filename and
                    // not a directory.
                    if (Path.GetFileName(fileOrFolderOutPath) == string.Empty)
                        return "Output path doesn't appear to be a valid file selection";
                }
            }

            return null;
        }
    }

}
