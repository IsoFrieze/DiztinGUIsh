using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Diz.Core.model;
using DiztinGUIsh.controller;
// using DiztinGUIsh.window;

namespace DiztinGUIsh
{
    public class ProjectSession
    {
        private Project Project;

        // private List<MainFormController> _controllers;
        // private MainFormController controller; // for now, just one. in the future, we can have multiple
        
        public event EventHandler Closed;

        public ProjectSession(string filename)
        {
            /*var window = new MainWindow
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

            controller.Show();*/
        }

        private void WindowOnClosed(object? sender, EventArgs e)
        {
            Closed?.Invoke(this, new EventArgs());
        }
    }
    
    // The class that handles the creation of the application windows
    internal class DizApplication : ApplicationContext
    {
        public List<ProjectSession> _sessions = new(); 
        
        public DizApplication(string openFile = "")
        {
            // Handle the ApplicationExit event to know when the application is exiting.
            Application.ApplicationExit += OnApplicationExit;

            var projectSession = new ProjectSession(openFile);
            projectSession.Closed += ProjectSessionOnClosed;
        }

        private void ProjectSessionOnClosed(object? sender, EventArgs e)
        {
            _sessions.Remove((ProjectSession)sender);
        }

        private void OnApplicationExit(object? sender, EventArgs e)
        {
            // cleanup if any
            _sessions.Clear();
        }
    }
}