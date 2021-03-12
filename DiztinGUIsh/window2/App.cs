using System.Collections.Generic;
using System.Windows.Forms;
using Diz.Core.model;
using DiztinGUIsh.controller;

namespace DiztinGUIsh.window2
{
    public class App
    {
        public FormsController FormsController { get; }= new ();
        public ProjectsController ProjectsController { get; } = new();

        public void OpenFileWithNewView(string filename)
        {
            var project = ProjectsController.OpenProject(filename);
            if (project == null)
                return;
            
            // dumb test. this should be the ASCII title of the game in the ROM header
            var startingOffset = 0xFFC0;
            var count = 0x15;
            
            // TODO: add real form here
        }
    }

    public class FormsController
    {
        public List<Form> Forms { get; }

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
    }

    public class ProjectsController
    {
        public Dictionary<string, Project> Projects { get; } = new();

        public Project OpenProject(string filename)
        {
            if (Projects.ContainsKey(filename))
                return Projects[filename];

            var project = ProjectOpenerGenericView.OpenProjectWithGui(filename);
            if (project == null)
                return null;

            Projects.Add(filename, project);
            return project;
        }
    }
}