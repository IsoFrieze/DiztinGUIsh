using System;
using Diz.Core.export;
using Diz.Core.model;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh.controller
{
    public interface IProjectView
    {
        Project Project { get; set; }
        void OnProjectOpenFail(string errorMsg);
        void OnProjectSaved();
        void OnExportFinished(LogCreator.OutputResult result);

        public delegate void LongRunningTaskHandler(Action task, string description = null);
        LongRunningTaskHandler TaskHandler { get; }
        int SelectedOffset { get; }
        void SelectOffset(int offset);
        string AskToSelectNewRomFilename(string promptSubject, string promptText);
        IImportRomDialogView GetImportView();
        void OnProjectOpenWarning(string warningMsg);
        void Show();

        bool PromptHarshAutoStep(int offset, out int newOffset, out int count);
        MarkManyDialog PromptMarkMany(int offset, int column);
        void ShowOffsetOutOfRangeMsg();
    }
}
