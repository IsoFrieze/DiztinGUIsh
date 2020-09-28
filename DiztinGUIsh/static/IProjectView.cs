using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    interface IProjectView
    {
        Project Project { get; set; }
        void OnProjectOpened(string filename);
        void OnProjectOpenFail();
        void OnProjectSaved();
        void OnExportFinished(LogCreator.OutputResult result);

        public delegate void LongRunningTaskHandler(Action task, string description = null);
        LongRunningTaskHandler TaskHandler { get; }
    }
}
