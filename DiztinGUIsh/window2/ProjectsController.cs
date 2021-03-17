using System;
using System.Collections.Generic;
using Diz.Core;
using Diz.Core.model;
using DiztinGUIsh.controller;

namespace DiztinGUIsh.window2
{
    public class ProjectsController
    {
        private Dictionary<string, Project> Projects { get; } = new();
        
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

    public class GlobalViewControllers
    {
        public List<IController> Controllers { get; } = new();

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
}