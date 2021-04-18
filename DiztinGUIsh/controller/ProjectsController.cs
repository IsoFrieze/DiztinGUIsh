using System;
using System.Collections.Generic;
using Diz.Core;
using Diz.Core.model;
using DiztinGUIsh.controller;
using DiztinGUIsh.Properties;

namespace DiztinGUIsh.controller
{
    public class ProjectsController
    {
        public Dictionary<string, Project> Projects { get; } = new();

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
            ProjectOpenerHandlerGenericHandler.OpenProjectWithGui(filename, showMessageBoxOnSuccess: false);
        
        // TODO: make this a list of last N projects opened
        // This property is intended to persist beyond application restart, so you can 
        // open the last filename you were working on.
        public static string LastOpenedProjectFilename
        {
            get => Settings.Default.LastOpenedFile;
            set
            {
                Settings.Default.LastOpenedFile = value;
                Settings.Default.Save();
            }
        }
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

            return new Project {
                Data = SampleRomData.SampleData,
                ProjectFileName = SampleProjectName
            };
        }
    }
}