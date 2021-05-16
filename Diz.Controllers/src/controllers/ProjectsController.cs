using System;
using System.Collections.Generic;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Controllers.controllers
{
    public class ProjectsController
    {
        private IProjectOpenerHandler ProjectOpenerView { get; set; }
        public Dictionary<string, Project> Projects { get; } = new();
        public static string LastOpenedProjectFilename { get; set; }

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
            ProjectOpenerView.OpenProject(filename, showMessageBoxOnSuccess: false);
    }

    public class GlobalViewControllers
    {
        public List<IFormController> Controllers { get; } = new();

        public void RegisterNewController(IFormController controller)
        {
            Controllers.Add(controller);
            controller.Closed += OnControllerClosed;
        }

        private void OnControllerClosed(object sender, EventArgs e)
        {
            Controllers.RemoveAll(c => ReferenceEquals(c, sender));
            if (Controllers.Count == 0)
                AllFormsClosed?.Invoke(this, new EventArgs());
        }
        public event EventHandler AllFormsClosed;
    }
    
    public class SampleRomHackProjectsController : ProjectsController
    {
        // ReSharper disable once MemberCanBeProtected.Global
        public const string SampleProjectName = "sampleproject111111112"; // temp hack.
        
        protected override Project ReadProject(string filename)
        {
            if (filename != SampleProjectName) 
                return base.ReadProject(filename);

            var project = new Project {
                Data = SampleRomData.CreateSampleData().Data,
            };

            project.Session = new ProjectSession(project)
            {
                ProjectFileName = SampleProjectName,
            };

            return project;
        }
    }
}