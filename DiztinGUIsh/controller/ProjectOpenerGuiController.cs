using System;
using System.IO;
using System.Linq;
using Diz.Core.model;
using Diz.Core.serialization;
using DiztinGUIsh.window2;

namespace DiztinGUIsh.controller
{
    public class ProjectOpenerGuiController
    {
        public IProjectOpenerHandler Handler { get; init; }

        public Project OpenProject(string filename)
        {
            Project project = null;
            var errorMsg = "";
            var warningMsg = "";

            DoLongRunningTask(() =>
            {
                try
                {
                    var (project1, warning) = new ProjectFileManager()
                    {
                        RomPromptFn = Handler.AskToSelectNewRomFilename
                    }.Open(filename);

                    project = project1;
                    warningMsg = warning;
                }
                catch (AggregateException ex)
                {
                    project = null;
                    errorMsg = ex.InnerExceptions.Select(e => e.Message).Aggregate((line, val) => line += val + "\n");
                }
                catch (Exception ex)
                {
                    project = null;
                    errorMsg = ex.Message;
                }
            }, $"Opening {Path.GetFileName(filename)}...");
            
            if (project == null)
            {
                Handler.OnProjectOpenFail(errorMsg);
                return null;
            }

            if (warningMsg != "")
                Handler.OnProjectOpenWarning(warningMsg);
            
            Handler.OnProjectOpenSuccess(filename, project);
            return project;
        }

        private void DoLongRunningTask(Action task, string description)
        {
            if (Handler.TaskHandler != null)
                Handler.TaskHandler(task, description);
            else
                task();
        }
    }
}