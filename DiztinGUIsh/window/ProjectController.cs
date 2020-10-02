using System;
using System.ComponentModel;
using System.IO;
using DiztinGUIsh.loadsave;

namespace DiztinGUIsh
{
    public class ProjectController
    {
        public IProjectView ProjectView { get; set; }
        public Project Project { get; private set; }

        public delegate void ProjectChangedEvent(object sender, ProjectChangedEventArgs e);
        public event ProjectChangedEvent ProjectChanged;

        public class ProjectChangedEventArgs
        {
            public enum ProjectChangedType {
                Invalid, Saved, Opened, Imported
            }

            public ProjectChangedType ChangeType;
            public Project Project;
            public string Filename;
        }

        // there's probably better ways to handle this.
        // probably replace with a UI like "start task" and "stop task"
        // so we can flip up a progress bar and remove it.
        public void DoLongRunningTask(Action task, string description = null)
        {
            if (ProjectView.TaskHandler != null)
                ProjectView.TaskHandler(task, description);
            else
                task();
        }

        public bool OpenProject(string filename)
        {
            Project project = null;

            DoLongRunningTask(delegate {
                project = ProjectFileManager.Open(filename, AskToSelectNewRomFilename);
            }, $"Opening {Path.GetFileName(filename)}...");

            if (project == null)
            {
                ProjectView.OnProjectOpenFail();
                return false;
            }

            OnProjectOpenSuccess(filename, project);
            return true;
        }

        private void OnProjectOpenSuccess(string filename, Project project)
        {
            ProjectView.Project = Project = project;
            Project.PropertyChanged += Project_PropertyChanged;

            ProjectChanged?.Invoke(this, new ProjectChangedEventArgs()
            {
                ChangeType = ProjectChangedEventArgs.ProjectChangedType.Opened,
                Filename = filename,
                Project = project,
            });
        }

        private void Project_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // TODO
        }

        public void SaveProject(string filename)
        {
            DoLongRunningTask(delegate
            {
                ProjectFileManager.Save(Project, filename);
            }, $"Saving {Path.GetFileName(filename)}...");
            ProjectView.OnProjectSaved();
        }

        public void ImportBizHawkCDL(string filename)
        {
            BizHawkCdl.Import(filename, Project.Data);

            ProjectChanged?.Invoke(this, new ProjectChangedEventArgs()
            {
                ChangeType = ProjectChangedEventArgs.ProjectChangedType.Imported,
                Filename = filename,
                Project = Project,
            });
        }

        public void ImportRomAndCreateNewProject(Project.ImportRomSettings importSettings)
        {
            var project = ProjectFileManager.ImportRomAndCreateNewProject(importSettings);
            OnProjectOpenSuccess(project.ProjectFileName, project);
        }

        private string AskToSelectNewRomFilename(string error)
        {
            return ProjectView.AskToSelectNewRomFilename("Error", $"{error} Link a new ROM now?");
        }

        public void WriteAssemblyOutput()
        {
            WriteAssemblyOutput(Project.LogWriterSettings);
        }

        private void WriteAssemblyOutput(LogWriterSettings settings)
        {
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

        public void MarkChanged()
        {
            // eventually set this via INotifyPropertyChanged or similar.
            Project.UnsavedChanges = true;
        }

        public void SelectOffset(int offset, int column = -1)
        {
            ProjectView.SelectOffset(offset, column);
        }
    }
}
