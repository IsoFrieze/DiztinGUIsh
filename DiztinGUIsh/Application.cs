using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Diz.Core;
using Diz.Core.model;
using DiztinGUIsh.controller;
using DiztinGUIsh.window2;

namespace DiztinGUIsh
{
    public class ProjectsController
    {
        public Dictionary<string, Project> Projects { get; } = new();
        
        // ReSharper disable once MemberCanBeProtected.Global
        public const string SampleProjectName = "sampleproject111111112"; // temp hack.

        public Project OpenProject(string filename)
        {
            if (Projects.ContainsKey(filename))
                return Projects[filename];
            
            var project = ReadProject(filename);
            if (project == null)
                return null;

            Projects.Add(filename, project);
            return project;
        }

        protected virtual Project ReadProject(string filename) => 
            ProjectOpenerGenericView.OpenProjectWithGui(filename);
    }

    public class SampleRomHackProjectsController : ProjectsController
    {
        protected override Project ReadProject(string filename)
        {
            if (filename != SampleProjectName) 
                return base.ReadProject(filename);

            return new Project {
                Data = SampleRomData.SampleData,
                ProjectFileName = SampleProjectName
            };
        }
    }

    // The class that handles the creation of the application windows
    public class DizApplication : ApplicationContext
    {
        private GlobalViewControllers GlobalViewControllers { get; } = new ();
        private ProjectsController ProjectsController { get; } = new SampleRomHackProjectsController();
        
        public void OpenProjectFileWithNewView(string filename)
        {
            var project = ProjectsController.OpenProject(filename);
            if (project == null)
                return;

            ShowNewProjectEditorForm(project);
        }
        
        public DizApplication(string openFile = "")
        {
            // Handle the ApplicationExit event to know when the application is exiting.
            Application.ApplicationExit += OnApplicationExit;
            GlobalViewControllers.AllFormsClosed += (o, args) => Application.Exit();
            
            // kick us off with the home screen
            ShowNewStartForm();
        }

        private void ShowNewStartForm()
        {
            var form = new StartForm();
            var controller = new StartFormDataBindingController
            {
                View = form,
                DizApplication = this
            };
            form.DataBindingController = controller;

            OnCreated(controller, form);
        }

        private void ShowNewProjectEditorForm(Project project)
        {
            var form = new DataGridEditorForm();
            var controller = new RomByteDataBindingGridFormController
            {
                View = form,
                Project = project,
                Data = project.Data,
            };
            form.DataController = controller;

            OnCreated(controller, form);
        }

        private void OnCreated(DataController controller, Control form)
        {
            GlobalViewControllers.RegisterNewController(controller);
            form.Show();
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            // cleanup
        }
    }

    public class GlobalViewControllers
    {
        private List<IController> Controllers { get; } = new();

        public void RegisterNewController(DataController controller)
        {
            Controllers.Add(controller);
            controller.Closed += OnControllerClosed;
        }

        private void OnControllerClosed(object sender, EventArgs e)
        {
            Controllers.Remove(sender as DataController);
            if (Controllers.Count == 0)
                AllFormsClosed?.Invoke(this, new EventArgs());
        }
        public event EventHandler AllFormsClosed;
    }
}

/*
var window = new MainWindow
{
    MainFormController = new MainFormController
    {
        Project = Project
    }
};
window.MainFormController.ProjectView = window;
controller = window.MainFormController;

window.Closed += WindowOnClosed;

if (filename != "")
    controller.OpenProject("");

controller.Show();
*/