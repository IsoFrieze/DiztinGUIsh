using System;
using Diz.Core.export;
using Diz.Core.model;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh.controller
{
    public interface IProjectView : ISnesNavigation
    {
        Project Project { get; set; }
        void OnProjectOpenFail(string errorMsg);
        void OnProjectSaved();
        void OnExportFinished(LogCreator.OutputResult result);

        public delegate void LongRunningTaskHandler(Action task, string description = null);
        LongRunningTaskHandler TaskHandler { get; }
        string AskToSelectNewRomFilename(string promptSubject, string promptText);
        IImportRomDialogView GetImportView();
        void OnProjectOpenWarning(string warningMsg);
    }

    public interface ISnesNavigation
    {
        void SelectOffset(int pcOffset, int column=-1, bool saveHistory = false);
    }
}
