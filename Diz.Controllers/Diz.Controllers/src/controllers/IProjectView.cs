using System;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.LogWriter;

namespace Diz.Controllers.controllers
{
    public interface ILongRunningTaskHandler
    {
        public delegate void LongRunningTaskHandler(Action task, string description, IProgressView progressView);
        LongRunningTaskHandler TaskHandler { get; }
    }
    
    public interface IMainGridWindowView : IProjectView, IFormViewer
    {
    
    }
    
    public interface IProjectView : ILongRunningTaskHandler, ISnesNavigation
    {
        Project Project { get; set; }
        void OnProjectOpenFail(string errorMsg);
        void OnProjectSaved();
        void OnExportFinished(LogCreatorOutput.OutputResult result);
        
        string AskToSelectNewRomFilename(string promptSubject, string promptText);
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
