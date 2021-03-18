using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using DiztinGUIsh.controller;
using DiztinGUIsh.window;
using DiztinGUIsh.window2;

namespace DiztinGUIsh
{
    public class DizApplication : ApplicationContext
    {
        private GlobalViewControllers GlobalViewControllers { get; } = new ();
        private ProjectsController ProjectsController { get; } = new SampleRomHackProjectsController();
        
        public void OpenProjectFileWithNewView(string filename)
        {
            var project = ProjectsController.OpenProject(filename);
            if (project == null)
                return;

            var controller = ShowNewProjectEditorForm();
            controller.SetProject(filename, project);
        }
        
        public DizApplication(string openFile = "")
        {
            // Handle the ApplicationExit event to know when the application is exiting.
            Application.ApplicationExit += OnApplicationExit;
            GlobalViewControllers.AllFormsClosed += (o, args) => Application.Exit();
            
            // kick us off with the home screen
            ShowNewStartForm();
            
            // temp hack
            openFile = "sampleproject111111112";

            if (!string.IsNullOrEmpty(openFile))
            {
                OpenProjectFileWithNewView(openFile);
            }
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

        private MainFormController ShowNewProjectEditorForm()
        {
            // var form = new DataGridEditorFormTemp();
            var form = new DataGridEditorForm();
            var controller = new MainFormController
            {
                ProjectView = form,
            };
            form.MainFormController = controller;

            OnCreated(controller, form);

            return controller;
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
    }
}