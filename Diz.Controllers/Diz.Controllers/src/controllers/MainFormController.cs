﻿/*using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Diz.Controllers.interfaces;
using Diz.Controllers.util;
using Diz.Core.commands;
using Diz.Core.export;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.serialization;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.Import.bizhawk;
using Diz.Import.bsnes.tracelog;
using Diz.Import.bsnes.usagemap;

namespace Diz.Controllers.controllers
{
    public class MainFormController : IMainFormController
    {
        private int selectedSnesOffset;
        public ILongRunningTaskHandler.LongRunningTaskHandler TaskHandler => ProgressBarJob.RunAndWaitForCompletion;

        public event EventHandler Closed;


        private readonly ProjectController projectController;
        
        
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

        private readonly IDataGridEditorForm dataGridEditorForm;
        private readonly IPercentDisassembledCalculator percentCompleteCalculator;

        public MainFormController(
            IDataGridEditorForm view, 
            ProjectController projectController, 
            IPercentDisassembledCalculator percentCompleteCalculator)
        {
            Debug.Assert(view != null);
            dataGridEditorForm = view;
            this.projectController = projectController;
            this.percentCompleteCalculator = percentCompleteCalculator;

            dataGridEditorForm.MainFormController = this;
            dataGridEditorForm.Closed += OnClosed;
            
            // percentCompleteCalculator.OnUpdatePercent += OnShouldUpdatePercent;
            // ScheduleUpdatePercentageUncovered();
        }

        private void OnShouldUpdatePercent(bool forceRecalculate)
        {
            // TODO
            // var calculationMsg = percentCompleteCalculator.
            // dataGridEditorForm.UpdatePercent();
            // percentComplete.Text = outputText;
        }

        // public void ScheduleUpdatePercentageUncovered()
        // {
        //     percentCompleteCalculator.StartCooldown(3); // 3 times x 2 seconds = 6 seconds.
        //
        //     // UpdatePercent() with a force refresh is very expensive so only do it when we're idle for a bit.
        //     dataGridEditorForm.UpdatePercent(); // less expensive invocation that just updates UI 
        // }
        
        private void OnClosed(object sender, EventArgs e) => Closed?.Invoke(this, e);
        
        public void Show() => dataGridEditorForm.Show();

        public Project Project { get; set; }

        public bool MoveWithStep { get; set; } = true;

        public FlagType CurrentMarkFlag { get; set; } = FlagType.Data8Bit;

        public void OpenProject(string filename)
        {
            projectController.OpenProject(filename);
        }
        
        public void OnProjectOpenSuccess(string filename, Project project)
        {
            SetProject(filename, project);
        }

        public void SetProject(string filename, Project project)
        {
            Project = project;
            Project.PropertyChanged += Project_PropertyChanged;
            
            
            
            ProjectController.ProjectChanged.Invoke(this, new IProjectController.ProjectChangedEventArgs
            {
                ChangeType = IProjectController.ProjectChangedEventArgs.ProjectChangedType.Opened,
                Filename = filename,
                Project = project,
            });
        }

        public IProjectController ProjectController { get; }

        public void OnProjectOpenWarning(string warnings) => 
            dataGridEditorForm.OnProjectOpenWarning(warnings);

        public void OnProjectOpenFail(string fatalError) => 
            dataGridEditorForm.OnProjectOpenFail(fatalError);

        public string AskToSelectNewRomFilename(string error) => 
            dataGridEditorForm.AskToSelectNewRomFilename("Error", $"{error} Link a new ROM now?");

        public Project OpenProject(string filename, bool showPopupAlertOnLoaded)
        {
            return new ProjectOpenerGuiController { Handler = this }
                .OpenProject(filename);
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
        
        private static ImportRomDialogController SetupImportController(IProjectView projectView)
        {
            // let the user select settings on the GUI
            var importController = new ImportRomDialogController {View = projectView.GetImportView()};
            importController.View.Controller = importController;
            return importController;
        }
        
        public bool ImportRomAndCreateNewProject(string romFilename)
        {
            var importController = SetupImportController(dataGridEditorForm);
            var importSettings = importController.PromptUserForImportOptions(romFilename);
            
            if (importSettings != null)
            {
                CloseProject();

                // actually do the import
                ImportRomAndCreateNewProject(importSettings);
                return true;
            }

            return false;
        }

        private void ImportRomAndCreateNewProject(ImportRomSettings importSettings)
        {
            var project = ImportUtils.ImportRomAndCreateNewProject(importSettings);
            OnProjectOpenSuccess(project.Session?.ProjectFileName, project);
        }

        public void WriteAssemblyOutput()
        {
            WriteAssemblyOutput(Project.LogWriterSettings);
        }


        public void UpdateExportSettings(LogWriterSettings selectedSettings) =>
            Project.LogWriterSettings = selectedSettings;

        public void MarkProjectAsUnsaved()
        {
            // eventually set this via INotifyPropertyChanged or similar,
            // instead of having to do it manually
            if (Project?.Session != null)
                Project.Session.UnsavedChanges = true;
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
        
        private void MarkMany(MarkCommand.MarkManyProperty markProperty, int markStart, object markValue, int markCount)
        {
            var snesApi = Project.Data.GetSnesApi();
            if (snesApi == null)
                return;
            
            var newNavigatedOffset = markProperty switch
            {
                MarkCommand.MarkManyProperty.Flag => snesApi.MarkTypeFlag(markStart, (FlagType) markValue, markCount),
                MarkCommand.MarkManyProperty.DataBank => snesApi.MarkDataBank(markStart, (int) markValue, markCount),
                MarkCommand.MarkManyProperty.DirectPage => snesApi.MarkDirectPage(markStart, (int) markValue, markCount),
                MarkCommand.MarkManyProperty.MFlag => snesApi.MarkMFlag(markStart, (bool) markValue, markCount),
                MarkCommand.MarkManyProperty.XFlag => snesApi.MarkXFlag(markStart, (bool) markValue, markCount),
                MarkCommand.MarkManyProperty.CpuArch => snesApi.MarkArchitecture(markStart, (Architecture) markValue, markCount),
                _ => -1
            };

            MarkProjectAsUnsaved();

            if (MoveWithStep && newNavigatedOffset != -1)
                SelectOffset(newNavigatedOffset, new ISnesNavigation.HistoryArgs {Description = "Mark (multi)"});
        }

        // public int MarkMany(int markProperty, int markStart, object markValue, int markCount)
        // {
        //     if (!RomDataPresent() || Project?.Data == null)
        //         return 0;
        //     
        //     var destination = markProperty switch
        //     {
        //         0 => Project.Data.MarkTypeFlag(markStart, (FlagType) markValue, markCount),
        //         1 => Project.Data.MarkDataBank(markStart, (int) markValue, markCount),
        //         2 => Project.Data.MarkDirectPage(markStart, (int) markValue, markCount),
        //         3 => Project.Data.MarkMFlag(markStart, (bool) markValue, markCount),
        //         4 => Project.Data.MarkXFlag(markStart, (bool) markValue, markCount),
        //         5 => Project.Data.MarkArchitecture(markStart, (Architecture) markValue, markCount),
        //         _ => 0
        //     };
        //
        //     MarkProjectAsUnsaved();
        //
        //     return destination;
        // }

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
            return Project?.Data?.Step(offset, branch, false, offset - 1) ?? 0;
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

        public int AutoStepHarsh(int offset, int count)
        {
            throw new NotImplementedException();
        }

        public void AutoStepHarsh(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            if (!dataGridEditorForm.PromptHarshAutoStep(offset, out var newOffset, out var count))
                return;

            MarkProjectAsUnsaved();
            
            var destinationOffset = DoAutoStep(newOffset, true, count);
            
            if (MoveWithStep) 
                SelectedSnesOffset = destinationOffset;
        }

        private int DoAutoStep(int newOffset, bool harsh, int count)
        {
            return Project?.Data?.AutoStep(newOffset, harsh, count) ?? 0;
        }

        public void Mark(int offset)
        {
            if (!RomDataPresent())
                return;
            
            MarkProjectAsUnsaved();
            var newOffset = Project?.Data?.MarkTypeFlag(
                offset, CurrentMarkFlag, RomUtil.GetByteLengthForFlag(CurrentMarkFlag)) ?? -1;
            
            if (newOffset != -1)
                SelectedSnesOffset = newOffset;
        }
        
        private int FindIntermediateAddress(int offset)
        {
            if (!RomDataPresent() || Project?.Data == null)
                return -1;

            var ia = Project.Data.GetIntermediateAddressOrPointer(offset);
            if (ia < 0)
                return -1;

            return Project.Data.ConvertSnesToPc(ia);
        }

        private bool FindUnreached(int offset, bool end, bool direction, out int unreached)
        {
            unreached = -1;
            if (!RomDataPresent() || Project?.Data == null)
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
            return Project?.Data?.GetFlag(offset) != FlagType.Unreached;
        }

        private bool IsUnreached(int offset)
        {
            return Project?.Data?.GetFlag(offset) == FlagType.Unreached;
        }

        public void MarkMany(int offset, int whichIndex)
        {
            if (!RomDataPresent()) 
                return;
            
            var markCmd = dataGridEditorForm.PromptMarkMany(offset, whichIndex);
            if (markCmd == null)
                return;

            var destination = MarkMany(markCmd.Property, markCmd.Start, markCmd.Value, markCmd.Count);

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

#if DIZ_3_BRANCH
        public void OnUserChangedSelection(ByteEntry newSelection)
        {
            // when user clicks on a new row in the child data grid editor, this fires
            SelectedSnesOffset = newSelection.ParentIndex;
        }
#endif

        private bool IsOffsetInRange(int offset)
        {
            if (!RomDataPresent())
                return false;
            
            return offset >= 0 && offset < Project?.Data?.GetRomSize();
        }

        public void GoTo(int offset)
        {
            if (IsOffsetInRange(offset))
                SelectedSnesOffset = offset;
            else
                dataGridEditorForm.ShowOffsetOutOfRangeMsg();
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
                Data.Labels.ImportLabelsFromCsv(importFilename, replaceAll, ref errLine);
                labelEditor.RepopulateFromData();
            }
            catch (Exception ex)
            {
                labelEditor.ShowLineItemError(ex.Message, errLine);
            }
        }

        public int Step(int offset, bool branch, bool force, int prevOffset)
        {
            throw new NotImplementedException();
        }

        int IAutoSteppable.AutoStepSafe(int offset)
        {
            throw new NotImplementedException();
        }
    }
}*/