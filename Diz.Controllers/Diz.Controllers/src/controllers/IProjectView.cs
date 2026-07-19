using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.LogWriter;

namespace Diz.Controllers.controllers
{
    public interface ILongRunningTaskHandler
    {
        // new-ui plan step 6: Task-based, no raw Thread/spin-wait. The handler (a UI edge)
        // shows its toolkit's progress view, runs `work` OFF the UI thread (Task.Run),
        // marshals progress back, and completes the returned Task when the work + UI teardown
        // are done. `work` receives an IProgress<int> (report 0..100 for a determinate bar)
        // and a CancellationToken; `isMarquee` selects the spinner vs. determinate style.
        // A null TaskHandler means "no UI" (headless): the caller runs the work inline.
        public delegate Task LongRunningTaskHandler(
            Action<IProgress<int>, CancellationToken> work,
            string description,
            bool isMarquee);
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
        void OnProjectOpenWarnings(IEnumerable<string> warnings);
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
        void SelectOffsetWithOvershoot(int pcOffset, int overshootAmount = 0);
        
        // get the PC offset of the currently selected row in the view
        public int SelectedOffset { get; }
    }
}
