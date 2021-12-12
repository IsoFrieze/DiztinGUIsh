using System;
using Diz.Core.export;
using Diz.Core.model;
using Diz.LogWriter;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh.controller
{
    public interface IProjectView : ISnesNavigation
    {
        Project Project { get; set; }
        void OnProjectOpenFail(string errorMsg);
        void OnProjectSaved();
        void OnExportFinished(LogCreatorOutput.OutputResult result);

        public delegate void LongRunningTaskHandler(Action task, string description = null);
        LongRunningTaskHandler TaskHandler { get; }
        string AskToSelectNewRomFilename(string promptSubject, string promptText);
        IImportRomDialogView GetImportView();
        void OnProjectOpenWarning(string warningMsg);
    }

    public interface ISnesNavigation
    {
        public class HistoryArgs
        {
            public string Description { get; set; }
            public string Position { get; set; }
        }
        
        /// <summary>
        /// Select a PC offset
        /// </summary>
        /// <param name="pcOffset">PC [not SNES] offset</param>
        /// <param name="historyArgs">if non-null, record this event in the project history</param>
        void SelectOffset(int pcOffset, HistoryArgs historyArgs = null);
    }
}
