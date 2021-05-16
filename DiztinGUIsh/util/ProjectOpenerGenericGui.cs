using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.model;

namespace DiztinGUIsh.util
{
    public class ProjectOpenerHandlerGenericHandler : IProjectOpenerHandler
    {
        public bool MessageboxShowOnProjectOpenSuccess { get; init; }= true;
        
        public ILongRunningTaskHandler.LongRunningTaskHandler TaskHandler =>
            ProgressBarJob.RunAndWaitForCompletion;

        public void OnProjectOpenSuccess(string filename, Project project)
        {
            if (MessageboxShowOnProjectOpenSuccess)
                MessageBox.Show("project file opened!");
        }

        public void OnProjectOpenWarning(string warnings)
        {
            MessageBox.Show($"project file opened, with warnings:\n {warnings}");
        }

        public void OnProjectOpenFail(string fatalError)
        {
            MessageBox.Show($"project file failed to open:\n {fatalError}");
        }

        public string AskToSelectNewRomFilename(string error)
        {
            string initialDir = null; // TODO: Project.ProjectFileName
            return GuiUtil.PromptToConfirmAction("Error", $"{error} Link a new ROM now?", 
                () => GuiUtil.PromptToSelectFile(initialDir)
            );
        }

        public Project OpenProject(string filename, bool showMessageBoxOnSuccess)
        {
            return OpenProjectWithGui(filename, showMessageBoxOnSuccess: false);
        }

        public static Project OpenProjectWithGui(string filename, bool showMessageBoxOnSuccess = true)
        {
            if (!GuiUtil.PromptScaryUnstableBetaAreYouSure())
                return null;
            
            return new ProjectOpenerGuiController
            {
                Handler = new ProjectOpenerHandlerGenericHandler
                {
                    MessageboxShowOnProjectOpenSuccess = showMessageBoxOnSuccess
                }
            }.OpenProject(filename);
        }
    }
}