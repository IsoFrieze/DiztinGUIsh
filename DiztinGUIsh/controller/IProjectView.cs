using System;
using Diz.Core.export;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh.controller
{
    public interface ILongRunningTaskHandler
    {
        public delegate void LongRunningTaskHandler(Action task, string description = null);
        LongRunningTaskHandler TaskHandler { get; }
    }
    
    public interface IProjectView : ILongRunningTaskHandler
    {
        void OnProjectOpenWarning(string warningMsg);
        void OnProjectOpenFail(string errorMsg);
        void OnExportFinished(LogCreatorOutput.OutputResult result);
        
        string AskToSelectNewRomFilename(string promptSubject, string promptText);
        IImportRomDialogView GetImportView();

        bool PromptHarshAutoStep(int offset, out int newOffset, out int count);
        MarkManyDialog PromptMarkMany(int offset, int whichIndex);
        void ShowOffsetOutOfRangeMsg();
    }
}
