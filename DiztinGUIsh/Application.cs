using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using DiztinGUIsh.controller;
using DiztinGUIsh.window;

namespace DiztinGUIsh
{
    public class DizApplicationContext : ApplicationContext
    {
        public class DizApplicationArgs
        {
            public string FileToOpen { get; set; }
        }
        
        public DizApplicationContext(DizApplicationArgs Args)
        {
            DizApplication.App.Run(Args);
        }
    }
    
    public class DizApplication
    {
        public GlobalViewControllers GlobalViewControllers { get; } = new ();
        public ProjectsController ProjectsController { get; } = new SampleRomHackProjectsController();
        
        private static DizApplication appInstance;

        public static DizApplication App => 
            appInstance ??= new DizApplication();

        public void OpenProjectFileWithNewView(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return;
            
            var project = ProjectsController.OpenProject(filename);
            if (project == null)
                return;

            var controller = ShowNewProjectEditorForm();
            controller.SetProject(filename, project);
        }

        public void Run(DizApplicationContext.DizApplicationArgs Args)
        {
            Application.ApplicationExit += OnApplicationExit;
            GlobalViewControllers.AllFormsClosed += (o, args) => Application.Exit();

            // kick us off with the home screen
            ShowNewStartForm();

            OpenProjectFileWithNewView(Args.FileToOpen);
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

        private MainFormController ShowNewProjectEditorForm()
        {
            var form = new DataGridEditorForm();
            var controller = new MainFormController
            {
                DataGridEditorForm = form,
            };
            form.MainFormController = controller;

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
            
            var controller = ShowNewProjectEditorForm();
            controller.SetProject("", openProject);
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            // cleanup
        }
    }
}