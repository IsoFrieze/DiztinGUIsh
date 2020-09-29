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
            {
                ProjectView.TaskHandler(task, description);
            }
            else
            {
                task();
            }
        }

        public bool OpenProject(string filename)
        {
            Project project = null;
            DoLongRunningTask(delegate {
                project = ProjectFileManager.Open(filename);
            }, $"Opening {Path.GetFileName(filename)}...");

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

        public void ImportRomAndCreateNewProject(in Project.ImportRomSettings importSettings)
        {
            var project = new Project
            {
                AttachedRomFilename = importSettings.rom_filename,
                UnsavedChanges = false,
                ProjectFileName = null,
                Data = new Data()
            };

            // TODO: seems like we probably should pick a place for the Project reference to live.
            // either here in this class, or out in the view.
            // right now we're passing around our class's Project and ProjectView's project.

            project.Data.Initiate(importSettings.rom_bytes, importSettings.ROMMapMode, importSettings.ROMSpeed);

            // TODO: get this UI out of here. probably just use databinding instead
            // AliasList.me.ResetDataGrid();

            if (importSettings.InitialLabels.Count > 0)
            {
                foreach (var pair in importSettings.InitialLabels)
                    project.Data.AddLabel(pair.Key, pair.Value, true);
                project.UnsavedChanges = true;
            }

            if (importSettings.InitialHeaderFlags.Count > 0)
            {
                foreach (var pair in importSettings.InitialHeaderFlags)
                    project.Data.SetFlag(pair.Key, pair.Value);
                project.UnsavedChanges = true;
            }

            // Save a copy of these identifying ROM bytes with the project file itself.
            // When we reload, we will make sure the linked ROM still matches them.
            project.InternalCheckSum = project.Data.GetRomCheckSumsFromRomBytes();
            project.InternalRomGameName = project.Data.GetRomNameFromRomBytes();
            
            OnProjectOpened(project.ProjectFileName, project);
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
