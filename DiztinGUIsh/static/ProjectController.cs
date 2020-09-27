using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiztinGUIsh.loadsave;

namespace DiztinGUIsh
{
    class ProjectController
    {
        public IProjectView ProjectView { get; set; }
        public Project Project { get; private set; }

        public bool OpenProject(string filename)
        {
            var project = ProjectFileManager.Open(filename);
            if (project == null)
            {
                ProjectView.OnProjectOpenFail();
                return false;
            }

            OnProjectOpened(filename, project);
            return true;
        }

        private void OnProjectOpened(string filename, Project project)
        {
            ProjectView.Project = Project = project;
            ProjectView.OnProjectOpened(filename);
        }

        public void SaveProject(string projectProjectFileName)
        {
            ProjectFileManager.Save(Project, Project.ProjectFileName);
            ProjectView.OnProjectSaved();
        }

        public void ImportRomAndCreateNewProject(Project.ImportRomSettings importSettings)
        {
            var project = new Project();
            project.ImportRomAndCreateNewProject(importSettings);

            // TODO: seems like we probably should pick a place for the Project reference to live.
            // either here in this class, or out in the view.
            // right now we're passing around our class's Project and ProjectView's project.

            OnProjectOpened(project.ProjectFileName, project);
        }

        public void WriteAssemblyOutput()
        {
            WriteAssemblyOutput(ref Project.LogWriterSettings);
        }

        private void WriteAssemblyOutput(ref LogWriterSettings settings)
        {
            int errors = 0;

            // kinda hate that we're passing in these...
            using var sw = new StreamWriter(settings.file);
            using var er = new StreamWriter(settings.error);
            
            var lc = new LogCreator()
            {
                Settings = settings,
                Data = Project.Data,
                StreamOutput = sw,
                StreamError = er,
            };

            var result = lc.CreateLog();

            if (result.error_count == 0)
                File.Delete(settings.error);

            ProjectView.OnExportFinished(result);
        }

        public void UpdateExportSettings(LogWriterSettings selectedSettings)
        {
            // TODO: ref readonly or similar here, to save us an extra copy of the struct.

            Project.LogWriterSettings = selectedSettings;
        }
    }
}
