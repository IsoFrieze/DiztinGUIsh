#nullable enable

using System;
using System.IO;
using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core;
using Diz.Core.model;
using Diz.Core.util;
using JetBrains.Annotations;
using LightInject;

namespace DiztinGUIsh
{
    public class DizApplicationContext : ApplicationContext
    {
        public DizApplicationContext(IDizApplication.Args args)
        {
            var app = Service.Container.GetInstance<IDizApplication>();
            app.Run(args);
        }
    }

    [UsedImplicitly]
    public class DizApplication : IDizApplication
    {
        public IControllersManager ControllersManager { get; } = new ControllersManager();
        public IProjectsManager ProjectsManager { get; }

        public DizApplication(IProjectsManager projectsManager)
        {
            Application.ApplicationExit += OnApplicationExit;
            
            ProjectsManager = projectsManager;
            ProjectsManager.OnProjectOpened += OnProjectOpened;
            
            ControllersManager.AllFormsClosed += (_, _) => Application.Exit();
            ControllersManager.ProjectOpenRequested += OnProjectOpenRequested;
        }
        
        public void Run(IDizApplication.Args args)
        {
            // kick us off with the home screen, this will return immediately
            ControllersManager.ShowNewStartForm();
            
            // if we're automatically opening a previous project file, do it now.
            ProjectsManager.OpenProjectFile(args.FileToOpen);
        }

        private void OnProjectOpenRequested(object? sender, ProjectOpenEventArgs e)
        {
            try
            {
                if (e.OpenLast)
                    ProjectsManager.OpenLastLoadedProject();
                else if (!string.IsNullOrEmpty(e.Filename))
                    ProjectsManager.OpenProjectFile(e.Filename);
            }
            catch (InvalidDataException exception)
            {
                OnError(exception.Message);
            }
        }

        public void OnProjectOpened([CanBeNull] object? sender, Project project) => 
            ControllersManager.ShowNewProjectEditorForm(project);

        private static void OnError(string msg) => MessageBox.Show(msg);

        private void OnApplicationExit(object? sender, EventArgs e)
        {
            // TODO: cleanup. release Projects, "are you sure you want to exit" etc.
        }
    }
}