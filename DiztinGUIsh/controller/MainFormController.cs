using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Diz.Core.export;
using Diz.Core.import;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.util;
using DiztinGUIsh.util;
using DiztinGUIsh.window;
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
//   MainFormController -> When the form needs to change any state, it talks to MainFormController
//                        i.e. when user clicks "Open Project", it sends the filename to us for handling
//   Project -> The actual data, the model. It knows nothing about GUI, just is the low-level business logic

namespace DiztinGUIsh.controller
{
    public class MainFormController
    {
        public IProjectView ProjectView { get; set; }
        public Project Project { get; set; }
        
        public bool MoveWithStep { get; set; } = true;

        public delegate void ProjectChangedEvent(object sender, ProjectChangedEventArgs e);

        public event ProjectChangedEvent ProjectChanged;

        public FlagType CurrentMarkFlag = FlagType.Data8Bit;

        public class ProjectChangedEventArgs
        {
            public enum ProjectChangedType
            {
                Invalid,
                Saved,
                Opened,
                Imported,
                Closing
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

            DoLongRunningTask(delegate
            {
                try
                {
                    var (project1, warning) = new ProjectFileManager()
                    {
                        RomPromptFn = AskToSelectNewRomFilename
                    }.Open(filename);

                    project = project1;
                    warningMsg = warning;
                }
                catch (AggregateException ex)
                {
                    project = null;
                    errorMsg = ex.InnerExceptions.Select(e => e.Message).Aggregate((line, val) => line += val + "\n");
                }
                catch (Exception ex)
                {
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
            DoLongRunningTask(delegate { new ProjectFileManager().Save(Project, filename); },
                $"Saving {Path.GetFileName(filename)}...");
            ProjectView.OnProjectSaved();
        }

        public void ImportBizHawkCdl(string filename)
        {
            BizHawkCdlImporter.Import(filename, Project.Data);

            ProjectChanged?.Invoke(this, new ProjectChangedEventArgs()
            {
                ChangeType = ProjectChangedEventArgs.ProjectChangedType.Imported,
                Filename = filename,
                Project = Project,
            });
        }

        public bool ImportRomAndCreateNewProject(string romFilename)
        {
            // let the user select settings on the GUI
            var importController = new ImportRomDialogController {View = ProjectView.GetImportView()};
            importController.View.Controller = importController;
            var importSettings = importController.PromptUserForRomSettings(romFilename);
            if (importSettings == null)
                return false;

            CloseProject();

            // actually do the import
            ImportRomAndCreateNewProject(importSettings);
            return true;
        }

        public void ImportRomAndCreateNewProject(ImportRomSettings importSettings)
        {
            var project = BaseProjectFileManager.ImportRomAndCreateNewProject(importSettings);
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
            if (!RomDataPresent())
                return;
            
            var lc = new LogCreator()
            {
                Settings = settings,
                Data = Project.Data,
            };

            LogCreator.OutputResult result = null;
            DoLongRunningTask(delegate { result = lc.CreateLog(); }, "Exporting assembly source code...");

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

        public long ImportBsnesUsageMap(string fileName)
        {
            if (!RomDataPresent())
                return 0L;
            
            var linesModified = BsnesUsageMapImporter.ImportUsageMap(File.ReadAllBytes(fileName), Project.Data);

            if (linesModified > 0)
                MarkChanged();

            return linesModified;
        }

        public long ImportBsnesTraceLogs(string[] fileNames)
        {
            if (!RomDataPresent())
                return 0L;
            
            var importer = new BsnesTraceLogImporter(Project.Data);

            // TODO: differentiate between binary-formatted and text-formatted files
            // probably look for a newline within 80 characters
            // call importer.ImportTraceLogLineBinary()

            // caution: trace logs can be gigantic, even a few seconds can be > 1GB
            // inside here, performance becomes critical.
            LargeFilesReader.ReadFilesLines(fileNames,
                (line) => { importer.ImportTraceLogLine(line); });

            if (importer.CurrentStats.NumRomBytesModified > 0)
                MarkChanged();

            return importer.CurrentStats.NumRomBytesModified;
        }

        public long ImportBsnesTraceLogsBinary(IEnumerable<string> filenames)
        {
            if (!RomDataPresent())
                return 0L;
            
            var importer = new BsnesTraceLogImporter(Project.Data);

            foreach (var file in filenames)
            {
                using Stream source = File.OpenRead(file);
                const int bytesPerPacket = 22;
                var buffer = new byte[bytesPerPacket];
                int bytesRead;
                while ((bytesRead = source.Read(buffer, 0, bytesPerPacket)) > 0)
                {
                    Debug.Assert(bytesRead == 22);
                    importer.ImportTraceLogLineBinary(buffer);
                }
            }

            return importer.CurrentStats.NumRomBytesModified;
        }

        public void CloseProject()
        {
            if (Project == null)
                return;

            ProjectChanged?.Invoke(this, new ProjectChangedEventArgs()
            {
                ChangeType = ProjectChangedEventArgs.ProjectChangedType.Closing
            });

            Project = null;
        }

        public int MarkMany(int markProperty, int markStart, object markValue, int markCount)
        {
            if (!RomDataPresent())
                return 0;
            
            var destination = markProperty switch
            {
                0 => Project.Data.MarkTypeFlag(markStart, (FlagType) markValue, markCount),
                1 => Project.Data.MarkDataBank(markStart, (int) markValue, markCount),
                2 => Project.Data.MarkDirectPage(markStart, (int) markValue, markCount),
                3 => Project.Data.MarkMFlag(markStart, (bool) markValue, markCount),
                4 => Project.Data.MarkXFlag(markStart, (bool) markValue, markCount),
                5 => Project.Data.MarkArchitecture(markStart, (Architecture) markValue, markCount),
                _ => 0
            };

            MarkChanged();

            return destination;
        }

        public void Show()
        {
            ProjectView?.Show();
        }

        private bool RomDataPresent() => Project?.Data?.GetRomSize() > 0;

        public void Step(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            MarkChanged();
            var destinationOffset = Project.Data.Step(offset, false, false, offset - 1);
            ProjectView.SelectOffset(destinationOffset);
            // RefreshPercentAndWindowTitle();
        }

        public void StepIn(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            MarkChanged();
            var destinationOffset = Project.Data.Step(offset, true, false, offset - 1);
            ProjectView.SelectOffset(destinationOffset);
            // RefreshPercentAndWindowTitle();
        }

        public void AutoStepSafe(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            MarkChanged();
            var destinationOffset = Project.Data.AutoStep(offset, false, 0);
            if (MoveWithStep) 
                ProjectView.SelectOffset(destinationOffset);
            
            // RefreshPercentAndWindowTitle();
        }

        public void AutoStepHarsh(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            if (!ProjectView.PromptHarshAutoStep(offset, out var newOffset, out var count))
                return;

            MarkChanged();
            var destination = Project.Data.AutoStep(newOffset, true, count);
            
            if (MoveWithStep) 
                ProjectView.SelectOffset(destination);

            // RefreshPercentAndWindowTitle();
        }

        public void Mark(int offset)
        {
            if (!RomDataPresent())
                return;
            
            MarkChanged();
            var newOffset = Project.Data.MarkTypeFlag(offset, CurrentMarkFlag, RomUtil.GetByteLengthForFlag(CurrentMarkFlag));
            
            ProjectView.SelectOffset(newOffset);
            
            // UpdateUI_Tmp3();
        }
        
        private int FindIntermediateAddress(int offset)
        {
            if (!RomDataPresent())
                return -1;

            var ia = Project.Data.GetIntermediateAddressOrPointer(offset);
            if (ia < 0)
                return -1;

            return Project.Data.ConvertSnesToPc(ia);
        }

        private bool FindUnreached(int offset, bool end, bool direction, out int unreached)
        {
            unreached = -1;
            if (!RomDataPresent())
                return false;
            
            var size = Project.Data.GetRomSize();
            unreached = end ? (direction ? 0 : size - 1) : offset;

            if (direction)
            {
                if (!end)
                    while (unreached < size - 1 && IsUnreached(unreached))
                        unreached++;
                
                while (unreached < size - 1 && IsReached(unreached)) 
                    unreached++;
            }
            else
            {
                if (unreached > 0) 
                    unreached--;
                
                while (unreached > 0 && IsReached(unreached)) 
                    unreached--;
            }

            while (unreached > 0 && IsUnreached(unreached - 1)) 
                unreached--;

            return IsUnreached(unreached);
        }

        private bool IsReached(int offset)
        {
            return Project.Data.GetFlag(offset) != FlagType.Unreached;
        }

        private bool IsUnreached(int offset)
        {
            return Project.Data.GetFlag(offset) == FlagType.Unreached;
        }

        public void ToggleMoveWithStep()
        {
            MoveWithStep = !MoveWithStep;
        }
        
        public void MarkMany(int offset, int column)
        {
            if (!RomDataPresent()) 
                return;
            
            var mark = ProjectView.PromptMarkMany(offset, column);
            if (mark == null)
                return;

            var destination = MarkMany(mark.Property, mark.Start, mark.Value, mark.Count);

            if (MoveWithStep)
                ProjectView.SelectOffset(destination);

            // RefreshTablePercentAndWindowTitle();
        }

        public void GoToIntermediateAddress(int offset)
        {
            if (!RomDataPresent())
                return;
            
            var snesOffset = FindIntermediateAddress(offset);
            if (snesOffset == -1)
                return;
            
            ProjectView.SelectOffset(snesOffset);
        }
        
        private bool IsOffsetInRange(int offset)
        {
            if (!RomDataPresent())
                return false;
            
            return offset >= 0 && offset < Project.Data.GetRomSize();
        }

        public void GoTo(int offset)
        {
            if (IsOffsetInRange(offset))
                ProjectView.SelectOffset(offset);
            else
                ProjectView.ShowOffsetOutOfRangeMsg();
        }

        public void GoToUnreached(bool end, bool direction)
        {
            if (!FindUnreached(ProjectView.SelectedOffset, end, direction, out var unreached))
                return;

            ProjectView.SelectOffset(unreached);
            // SelectOffset(unreached, 1);
        }

        public void SetMFlag(int offset, bool b)
        {
            Project.Data.SetMFlag(offset, b);
        }

        public void SetXFlag(int offset, bool b)
        {
            Project.Data.SetXFlag(offset ,b);
        }

        public void AddLabel(int convertPCtoSnes, Label label, bool b)
        {
            Project.Data.AddLabel(convertPCtoSnes, label, b);
        }

        public void SelectOffset(int offset)
        {
            ProjectView.SelectOffset(offset);
        }

        public void AddComment(int i, string s, bool overwrite)
        {
            Project.Data.AddComment(i, s, overwrite);
        }
    }
}