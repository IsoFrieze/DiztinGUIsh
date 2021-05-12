using Diz.Core.export;
using Diz.Core.util;

namespace Diz.LogWriter.util
{
    public static class LogUtil
    {
        /// <summary>
        /// Generate a sample of assembly output with the given settings
        /// </summary>
        /// <param name="baseSettings">Existing settings to base this generation on</param>
        /// <returns>Output of assembly generation as text</returns>
        /// <remarks>
        /// This is handy for UI and other areas where you want to quickly demo what the effect
        /// will be of various setting changes. We'll use a built-in sample ROM as our data source.
        /// </remarks>
        public static LogCreatorOutput.OutputResult GetSampleAssemblyOutput(LogWriterSettings baseSettings)
        {
            var sampleRomData = SampleRomData.CreateSampleData();
            var lc = new LogCreator
            {
                Settings = baseSettings with
                {
                    Structure = LogWriterSettings.FormatStructure.SingleFile,
                    FileOrFolderOutPath = "",
                    OutputToString = true,
                    RomSizeOverride = sampleRomData.OriginalRomSizeBeforePadding,
                },
                Data = sampleRomData.Data,
            };
            
            return lc.CreateLog();
        }
    }
}