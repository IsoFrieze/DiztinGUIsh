using System;
using System.ComponentModel;
using System.IO;
using Diz.Core.export;
using Diz.Core.import;
using Diz.Core.model;
using Diz.Core.serialization;
using DiztinGUIsh.util;
using DiztinGUIsh.window.dialog;

// Model-View-Controller architecture.
// goal: while this class's purpose is to be the middleman between dumb GUI elements and 
// our underlying data, we should strive to keep all direct GUI stuff out of this class.
// i.e. it's OK for this class to deal with confirming the user wants to do something,
// but not OK to directly use GUI functions to show a form. instead, it should reach
// out to the view classes and ask them to do things like popup dialog boxes/change form elements/etc.
//
// The idea here is that because there's no direct GUI stuff going on, we can run automation
// and unit testing on this class, and eventually add undo/redo support
//
// Where possible, let the GUI elements (forms) subscribe to data notifications on our model
// instead of trying to middleman everything.
//
// This separation of concerns isn't perfect yet, but, keep it in mind as you add functionality.
//
// example:
//   ProjectView -> A form that displays an opened project to the user
//   ProjectController -> When the form needs to change any state, it talks to ProjectController
//                        i.e. when user clicks "Open Project", it sends the filename to us for handling
//   Project -> The actual data, the model. It knows nothing about GUI, just is the low-level business logic

namespace DiztinGUIsh.controller
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
            var errorMsg = "";
            var warningMsg = "";

            // TODO: try/catch for ProjectFileManager
            DoLongRunningTask(delegate {
                try
                {
                    var result = ProjectFileManager.Open(filename, AskToSelectNewRomFilename);
                    project = result.project;
                    warningMsg = result.warning;
                } catch (Exception ex) {
                    project = null;
                    errorMsg = ex.Message;
                }
            }, $"Opening {Path.GetFileName(filename)}...");

            if (project == null)
            {
                ProjectView.OnProjectOpenFail(errorMsg);
                return false;
            }

            if (warningMsg != "")
                ProjectView.OnProjectOpenWarning(warningMsg);

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
            // TODO: use this to listen to interesting change events in Project/Data
            // so we can react appropriately.
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
            BizHawkCdlImporter.Import(filename, Project.Data);

            ProjectChanged?.Invoke(this, new ProjectChangedEventArgs()
            {
                ChangeType = ProjectChangedEventArgs.ProjectChangedType.Imported,
                Filename = filename,
                Project = Project,
            });
        }

        public bool ImportRomAndCreateNewProject(string ROMFilename)
        {
            // let the user select settings on the GUI
            var importController = new ImportROMDialogController {View = ProjectView.GetImportView()};
            importController.View.Controller = importController;
            var importSettings = importController.PromptUserForRomSettings(ROMFilename);
            if (importSettings == null)
                return false;

            // actually do the import
            ImportRomAndCreateNewProject(importSettings);
            return true;
        }

        public void ImportRomAndCreateNewProject(ImportRomSettings importSettings)
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
            WriteAssemblyOutput(Project.LogWriterSettings, true);
        }

        private void WriteAssemblyOutput(LogWriterSettings settings, bool showProgressBarUpdates = false)
        {
            var lc = new LogCreator()
            {
                Settings = settings,
                Data = Project.Data,
            };

            LogCreator.OutputResult result = null;
            DoLongRunningTask(delegate {
                result = lc.CreateLog();
            }, "Exporting assembly source code...");

            ProjectView.OnExportFinished(result);
        }

        public void UpdateExportSettings(LogWriterSettings selectedSettings)
        {
            // TODO: ref readonly or similar here, to save us an extra copy of the struct?

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

        public long ImportBSNESUsageMap(string fileName)
        {
            var importer = new BSNESUsageMapImporter();

            var linesModified = importer.ImportUsageMap(File.ReadAllBytes(fileName), Project.Data);

            if (linesModified > 0)
                MarkChanged();

            return linesModified;
        }

        public long ImportBSNESTraceLogs(string[] fileNames)
        {
            var totalLinesSoFar = 0L;

            var importer = new BSNESTraceLogImporter();

            // caution: trace logs can be gigantic, even a few seconds can be > 1GB
            // inside here, performance becomes critical.
            LargeFilesReader.ReadFilesLines(fileNames, delegate (string line)
            {
                totalLinesSoFar += importer.ImportTraceLogLine(line, Project.Data);
            });

            if (totalLinesSoFar > 0)
                MarkChanged();

            return totalLinesSoFar;
        }
    }
}
