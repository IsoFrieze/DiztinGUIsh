using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Diz.Core.arch;
using Diz.Core.export;
using Diz.Core.import;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.serialization;
using Diz.Core.util;
using DiztinGUIsh.util;
using DiztinGUIsh.window;
using DiztinGUIsh.window.dialog;
using DiztinGUIsh.window2;
using Label = Diz.Core.model.Label;

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
    public class MainFormController : IMainFormController
    {
        private FlagType currentMarkFlag = FlagType.Data8Bit;
        private int selectedSnesOffset;
        private IDataGridEditorForm dataGridEditorForm;
        public ILongRunningTaskHandler.LongRunningTaskHandler TaskHandler => ProgressBarJob.RunAndWaitForCompletion;

        public int SelectedSnesOffset
        {
            get => selectedSnesOffset;
            set
            {
                selectedSnesOffset = value;
                
                // TODO: event propagation/etc
            }
        }

        public Data Data => Project?.Data;

        public event IProjectController.ProjectChangedEvent ProjectChanged;

        public IDataGridEditorForm DataGridEditorForm
        {
            get => dataGridEditorForm;
            set
            {
                if (dataGridEditorForm != null)
                    dataGridEditorForm.Closed -= OnClosed;
                
                dataGridEditorForm = value;

                if (dataGridEditorForm != null)
                    dataGridEditorForm.Closed += OnClosed;
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            Closed?.Invoke(this, e);
        }

        public IProjectView ProjectView => DataGridEditorForm;
        public IFormViewer FormView => DataGridEditorForm;
        public IViewer View => DataGridEditorForm;
        public event EventHandler Closed;

        public Project Project { get; set; }

        public bool MoveWithStep { get; set; } = true;

        public FlagType CurrentMarkFlag
        {
            get => currentMarkFlag;
            set
            {
                currentMarkFlag = value;
            }
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

        public void OpenProject(string filename)
        {
            new ProjectOpenerGuiController { Handler = this }
                .OpenProject(filename);
        }
        
        public void OnProjectOpenSuccess(string filename, Project project)
        {
            SetProject(filename, project);
        }

        public void SetProject(string filename, Project project)
        {
            Project = project;
            Project.PropertyChanged += Project_PropertyChanged;
            
            ProjectChanged?.Invoke(this, new IProjectController.ProjectChangedEventArgs
            {
                ChangeType = IProjectController.ProjectChangedEventArgs.ProjectChangedType.Opened,
                Filename = filename,
                Project = project,
            });
        }

        public void OnProjectOpenWarning(string warnings) => 
            ProjectView.OnProjectOpenWarning(warnings);

        public void OnProjectOpenFail(string fatalError) => 
            ProjectView.OnProjectOpenFail(fatalError);

        public string AskToSelectNewRomFilename(string error) => 
            ProjectView.AskToSelectNewRomFilename("Error", $"{error} Link a new ROM now?");

        private void Project_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // TODO: use this to listen to interesting change events in Project/Data
            // so we can react appropriately.
        }

        public void SaveProject(string filename)
        {
            DoLongRunningTask(delegate { new ProjectFileManager().Save(Project, filename); },
                $"Saving {Path.GetFileName(filename)}...");
        }

        public void ImportBizHawkCdl(string filename)
        {
            BizHawkCdlImporter.Import(filename, Project.Data);

            ProjectChanged?.Invoke(this, new IProjectController.ProjectChangedEventArgs()
            {
                ChangeType = IProjectController.ProjectChangedEventArgs.ProjectChangedType.Imported,
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

        public void WriteAssemblyOutput()
        {
            WriteAssemblyOutput(Project.LogWriterSettings);
        }

        private void WriteAssemblyOutput(LogWriterSettings settings)
        {
            if (!RomDataPresent())
                return;
            
            var lc = new LogCreator()
            {
                Settings = settings,
                Data = Project.Data,
            };

            LogCreatorOutput.OutputResult result = null;
            DoLongRunningTask(delegate { result = lc.CreateLog(); }, "Exporting assembly source code...");

            ProjectView.OnExportFinished(result);
        }

        public void UpdateExportSettings(LogWriterSettings selectedSettings)
        {
            // TODO: ref readonly or similar here, to save us an extra copy of the struct?

            Project.LogWriterSettings = selectedSettings;
        }

        public void MarkProjectAsUnsaved()
        {
            // eventually set this via INotifyPropertyChanged or similar, instead of having to do it
            // manually
            Project.UnsavedChanges = true;
        }

        public long ImportBsnesUsageMap(string fileName)
        {
            if (!RomDataPresent())
                return 0L;
            
            var linesModified = BsnesUsageMapImporter.ImportUsageMap(File.ReadAllBytes(fileName), Project.Data);

            if (linesModified > 0)
                MarkProjectAsUnsaved();

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
                MarkProjectAsUnsaved();

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

            ProjectChanged?.Invoke(this, new IProjectController.ProjectChangedEventArgs()
            {
                ChangeType = IProjectController.ProjectChangedEventArgs.ProjectChangedType.Closing
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

            MarkProjectAsUnsaved();

            return destination;
        }

        private bool RomDataPresent() => Project?.Data?.GetRomSize() > 0;

        public void Step(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            MarkProjectAsUnsaved();
            SelectedSnesOffset = Step(offset, false);
        }

        public void StepIn(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            MarkProjectAsUnsaved();
            SelectedSnesOffset = Step(offset, true);
        }

        private int Step(int offset, bool branch)
        {
            return Project.Data.Step(offset, branch, false, offset - 1);
        }

        public void AutoStepSafe(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            MarkProjectAsUnsaved();

            var destinationOffset = DoAutoStep(offset, false, 0);

            if (MoveWithStep) 
                SelectedSnesOffset = destinationOffset;
        }

        public void AutoStepHarsh(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            if (!ProjectView.PromptHarshAutoStep(offset, out var newOffset, out var count))
                return;

            MarkProjectAsUnsaved();
            
            var destinationOffset = DoAutoStep(newOffset, true, count);
            
            if (MoveWithStep) 
                SelectedSnesOffset = destinationOffset;
        }

        private int DoAutoStep(int newOffset, bool harsh, int count)
        {
            return Project.Data.AutoStep(newOffset, harsh, count);
        }

        public void Mark(int offset)
        {
            if (!RomDataPresent())
                return;
            
            MarkProjectAsUnsaved();
            var newOffset = Project.Data.MarkTypeFlag(offset, CurrentMarkFlag, RomUtil.GetByteLengthForFlag(CurrentMarkFlag));
            
            SelectedSnesOffset = newOffset;
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

        public void MarkMany(int offset, int whichIndex)
        {
            if (!RomDataPresent()) 
                return;
            
            var mark = ProjectView.PromptMarkMany(offset, whichIndex);
            if (mark == null)
                return;

            var destination = MarkMany(mark.Property, mark.Start, mark.Value, mark.Count);

            if (MoveWithStep)
                SelectedSnesOffset = destination;
        }

        public void GoToIntermediateAddress(int offset)
        {
            if (!RomDataPresent())
                return;
            
            var snesOffset = FindIntermediateAddress(offset);
            if (snesOffset == -1)
                return;
            
            SelectedSnesOffset = snesOffset;
        }

        public void OnUserChangedSelection(ByteEntry newSelection)
        {
            // when user clicks on a new row in the child data grid editor, this fires
            SelectedSnesOffset = newSelection.ContainerOffset;
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
                SelectedSnesOffset = offset;
            else
                ProjectView.ShowOffsetOutOfRangeMsg();
        }

        public void GoToUnreached(bool end, bool direction)
        {
            if (!FindUnreached(SelectedSnesOffset, end, direction, out var unreached))
                return;

            SelectedSnesOffset = unreached;
        }
    
        public void SetDataBank(int romOffset, int result)
        {
            Data?.SetDataBank(romOffset, result);
        }
    
        public void SetDirectPage(int romOffset, int result)
        {
            Data?.SetDirectPage(romOffset, result);
        }
    
        public void SetMFlag(int romOffset, bool value)
        {
            Data?.SetMFlag(romOffset, value);
        }
    
        public void SetXFlag(int romOffset, bool value)
        {
            Data?.SetXFlag(romOffset, value);
        }

        public void ImportLabelsCsv(ILabelEditorView labelEditor, bool replaceAll)
        {
            var importFilename = labelEditor.PromptForCsvFilename();
            if (string.IsNullOrEmpty(importFilename))
                return;

            var errLine = 0;
            try
            {
                Data.LabelProvider.ImportLabelsFromCsv(importFilename, replaceAll, ref errLine);
                labelEditor.RepopulateFromData();
            }
            catch (Exception ex)
            {
                labelEditor.ShowLineItemError(ex.Message, errLine);
            }
        }
    }
}