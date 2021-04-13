using System.Windows.Forms;
using Diz.Core.model;
using DiztinGUIsh.util;
using DiztinGUIsh.window2;

namespace DiztinGUIsh.controller
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

        public static Project OpenProjectWithGui(string filename, bool showMessageBoxOnSuccess = true) => 
            new ProjectOpenerGuiController
            {
                Handler = new ProjectOpenerHandlerGenericHandler
                {
                    MessageboxShowOnProjectOpenSuccess = showMessageBoxOnSuccess
                }
            }.OpenProject(filename);
    }
}