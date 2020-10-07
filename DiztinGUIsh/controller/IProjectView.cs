using System;
using Diz.Core.export;
using Diz.Core.model;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh.controller
{
    public interface IProjectView
    {
        Project Project { get; set; }
        void OnProjectOpenFail();
        void OnProjectSaved();
        void OnExportFinished(LogCreator.OutputResult result);

        public delegate void LongRunningTaskHandler(Action task, string description = null);
        LongRunningTaskHandler TaskHandler { get; }
        void SelectOffset(int offset, int column=-1);
        string AskToSelectNewRomFilename(string promptSubject, string promptText);
        IImportRomDialogView GetImportView();
    }
}
