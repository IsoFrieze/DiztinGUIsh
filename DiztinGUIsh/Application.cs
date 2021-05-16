using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core;
using Diz.Core.util;
using DiztinGUIsh.window;
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

    public class DizApplication : IDizApplication
    {
        public GlobalViewControllers GlobalViewControllers { get; } = new ();
        public ProjectsController ProjectsController { get; } = new SampleRomHackProjectsController();

        public void OpenProjectFileWithNewView(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return;
            
            var project = ProjectsController.OpenProject(filename);
            if (project == null)
                return;

            ShowNewProjectEditorForm(controller => controller.SetProject(filename, project));
        }

        public void Run(IDizApplication.Args args)
        {
            Application.ApplicationExit += OnApplicationExit;
            GlobalViewControllers.AllFormsClosed += (_, _) => Application.Exit();

            // kick us off with the home screen
            ShowNewStartForm();

            OpenProjectFileWithNewView(args.FileToOpen);
        }

        private void ShowNewStartForm()
        {
            var form = new StartForm();
            var controller = new StartFormController
            {
                FormView = form,
            };
            form.Controller = controller;

            OnCreated(controller, form);
        }
        
        private MainFormController ShowNewProjectEditorForm(Action<MainFormController> beforeShow = null)
        {
            var form = new DataGridEditorForm();
            var controller = new MainFormController
            {
                DataGridEditorForm = form,
            };
            form.MainFormController = controller;
            
            beforeShow?.Invoke(controller);
            OnCreated(controller, form);

            return controller;
        }

        private void OnCreated(IFormController controller, Control form)
        {
            GlobalViewControllers.RegisterNewController(controller);
            form.Show();
        }

        public void OpenNewViewOfLastLoadedProject()
        {
            // hack. for now, only make this work with the first item.
            // in the future, implement this with an "currently opened projects" selection dialog 
            Debug.Assert(ProjectsController.Projects.Values.Count == 1);
            var openProject = ProjectsController.Projects.Values.Select(project => project).FirstOrDefault();
            if (openProject == null)
            {
                MessageBox.Show("Err: No open projects, or more than one. Need exactly one.");
                return;
            }
            
            ShowNewProjectEditorForm(controller => controller.SetProject("", openProject));
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            // cleanup
        }
    }
}