using System;
using Diz.Controllers.interfaces;
using Diz.Core.commands;
using Diz.LogWriter;

namespace Diz.Controllers.controllers
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
        MarkCommand PromptMarkMany(int offset, int whichIndex);
        void ShowOffsetOutOfRangeMsg();
    }
}
