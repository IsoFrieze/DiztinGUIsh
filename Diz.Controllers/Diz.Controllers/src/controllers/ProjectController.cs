using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Diz.Controllers.interfaces;
using Diz.Core;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.Import;
using Diz.Import.bizhawk;
using Diz.Import.bsnes.tracelog;
using Diz.Import.bsnes.usagemap;
using Diz.LogWriter;
using Diz.LogWriter.util;
using JetBrains.Annotations;

namespace Diz.Controllers.controllers;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class ProjectController : IProjectController
{
    public IProjectView ProjectView { get; set; }
    public Project Project { get; private set; }

    private readonly ICommonGui commonGui;
    private readonly IFilesystemService fs;
    private readonly IControllerFactory controllerFactory;
    private readonly Func<IProjectFileManager> projectFileManagerCreate;
    private readonly Func<ImportRomSettings, IProjectFactoryFromRomImportSettings> projectImporterFactoryCreate;
    private readonly Func<IProgressView> progressViewFactoryCreate;

    public ProjectController(
        ICommonGui commonGui,
        IFilesystemService fs,
        IControllerFactory controllerFactory,
        Func<ImportRomSettings, IProjectFactoryFromRomImportSettings> projectImporterFactoryCreate,
        Func<IProjectFileManager> projectFileManagerCreate,
        Func<IProgressView> progressViewFactoryCreate
    ) {
        this.commonGui = commonGui;
        this.fs = fs;
        this.controllerFactory = controllerFactory;
        this.projectImporterFactoryCreate = projectImporterFactoryCreate;
        this.projectFileManagerCreate = projectFileManagerCreate;
        this.progressViewFactoryCreate = progressViewFactoryCreate;
    }

    public event IProjectController.ProjectChangedEvent ProjectChanged;

    // there's probably better ways to handle this.
    // probably replace with a UI like "start task" and "stop task"
    // so we can flip up a progress bar and remove it.
    public void DoLongRunningTask(Action task, string description = null)
    {
        if (ProjectView.TaskHandler == null)
        {
            // fallback
            task();
            return;
        }

        // normal way to do it:
        var progressBarView = progressViewFactoryCreate();
        ProjectView.TaskHandler(task, description, progressBarView);
    }

    public bool OpenProject(string filename)
    {
        ProjectOpenResult projectOpenResult = null;
        var errorMsg = "";

        DoLongRunningTask(delegate
        {
            try
            {
                projectOpenResult = CreateProjectFileManager().Open(filename);
            }
            catch (AggregateException ex)
            {
                projectOpenResult = null;
                errorMsg = ex.InnerExceptions.Select(e => e.Message).Aggregate((line, val) => line += val + "\n");
            }
            catch (Exception ex)
            {
                projectOpenResult = null;
                errorMsg = ex.Message;
            }
        }, $"Opening {Path.GetFileName(filename)}...");

        if (projectOpenResult == null)
        {
            ProjectView.OnProjectOpenFail(errorMsg);
            return false;
        }

        var warnings = projectOpenResult.OpenResult.Warnings;
        if (warnings.Count > 0)
            ProjectView.OnProjectOpenWarnings(warnings);

        OnProjectOpenSuccess(filename, projectOpenResult.Root.Project);
        return true;
    }

    private IProjectFileManager CreateProjectFileManager()
    {
        var projectFileManager = projectFileManagerCreate();
        projectFileManager.RomPromptFn = AskToSelectNewRomFilename;
        return projectFileManager;
    }

    private void OnProjectOpenSuccess(string filename, Project project)
    {
        ProjectView.Project = Project = project;
        Project.PropertyChanged += Project_PropertyChanged;

        ProjectChanged?.Invoke(this, new IProjectController.ProjectChangedEventArgs
        {
            ChangeType = IProjectController.ProjectChangedEventArgs.ProjectChangedType.Opened,
            Filename = filename,
            Project = project,
        });
    }

    private void Project_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // TODO: use this to listen to interesting change events in Project/Data
        // so we can react appropriately.
    }

    public string SaveProject(string filename)
    {
        try
        {
            var emptyFilename = string.IsNullOrEmpty(filename);
            if (emptyFilename)
                throw new ArgumentException("empty filename specified", nameof(filename));

            string err = null;
            DoLongRunningTask(
                () => err = CreateProjectFileManager().Save(Project, filename),
                $"Saving {Path.GetFileName(filename)}..."
            );

            if (err != null)
                return err;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        ProjectView.OnProjectSaved();
        return null;
    }

    public void ImportBizHawkCdl(string filename)
    {
        BizHawkCdlImporter.Import(filename, Project.Data.GetSnesApi() ?? throw new InvalidOperationException("Project has no SNES API Present"));

        ProjectChanged?.Invoke(this, new IProjectController.ProjectChangedEventArgs
        {
            ChangeType = IProjectController.ProjectChangedEventArgs.ProjectChangedType.Imported,
            Filename = filename,
            Project = Project,
        });
    }

    public bool ImportRomAndCreateNewProject(string romFilename)
    {
        var importController = SetupImportController();
        var importSettings = importController.PromptUserForImportOptions(romFilename);
        if (importSettings == null) 
            return false;
        
        CloseProject();
        ImportRomAndCreateNewProject(importSettings);
        return true;
    }

    private void ImportRomAndCreateNewProject(ImportRomSettings importSettings)
    {
        var importer = projectImporterFactoryCreate.Invoke(importSettings);
        var project = importer.Read();
        if (project != null)
        {
            OnProjectOpenSuccess(project.ProjectFileName, project);   
        }
    }

    private IImportRomDialogController SetupImportController()
    {
        // let the user select settings on the GUI
        var importController = controllerFactory.GetImportRomDialogController();
        importController.View.Controller = importController;
        return importController;
    }

    public void ImportLabelsCsv(ILabelEditorView labelEditor, bool replaceAll)
    {
        var importFilename = labelEditor.PromptForCsvFilename();
        if (string.IsNullOrEmpty(importFilename))
            return;

        var errLine = 0;
        try
        {
            Project.Data.Labels.ImportLabelsFromCsv(importFilename, replaceAll, smartMerge: true, out errLine);
            labelEditor.RepopulateFromData();
        }
        catch (Exception ex)
        {
            labelEditor.ShowLineItemError(ex.Message, errLine);
        }
    }
    
    private string AskToSelectNewRomFilename(string error) => 
        ProjectView.AskToSelectNewRomFilename("Error", $"{error} Link a new ROM now?");

    public void WriteAssemblyOutput()
    {
        WriteAssemblyOutput(Project.LogWriterSettings, true);
    }

    private void WriteAssemblyOutput(LogWriterSettings settings, bool showProgressBarUpdates = false)
    {
        var lc = new LogCreator
        {
            Settings = settings,
            Data = new LogCreatorByteSource(Project.Data),
        };

        LogCreatorOutput.OutputResult result = null;
        DoLongRunningTask(() => result = lc.CreateLog(), "Exporting assembly source code...");

        ProjectView.OnExportFinished(result);
    }

    public void UpdateExportSettings(LogWriterSettings selectedSettings)
    {
        if (Project == null)
            return;
            
        var projectHadUnsavedChanges = Project.Session?.UnsavedChanges ?? false;
        var exportSettingsChanged = !Project.LogWriterSettings.Equals(selectedSettings);

        Project.LogWriterSettings = selectedSettings;

        if (Project.Session != null && exportSettingsChanged && !projectHadUnsavedChanges)
            Project.Session.UnsavedChanges = true;
    }

    public void MarkChanged()
    {
        // eventually set this via INotifyPropertyChanged or similar.
        if (Project.Session != null) Project.Session.UnsavedChanges = true;
    }

    public void SelectOffset(int offset, [CanBeNull] ISnesNavigation.HistoryArgs historyArgs = null) =>
        ProjectView.SelectOffset(offset, historyArgs);
    
    
    public int FixMisalignedFlags()
    {
        var countModified = Project.Data.GetSnesApi()?.FixMisalignedFlags() ?? 0;
        if (countModified > 0)
            MarkChanged();
        
        return countModified;
    }
    
    public bool RescanForInOut()
    {
        var snesData = Project.Data.GetSnesApi();
        if (snesData == null)
            return false;
        
        snesData.RescanInOutPoints();
        MarkChanged();
        return true;
    }

    public long ImportBsnesUsageMap(string fileName)
    {
        var snesData = Project?.Data.GetSnesApi();
        if (snesData == null)
            return 0;

        var linesModified = 0;
        DoLongRunningTask(() =>
        {
            // 1. run the BSNES import usage map
            var importer = new BsnesUsageMapImporter(
                usageMap: File.ReadAllBytes(fileName), 
                snesData: snesData,
                onlyMarkIfUnreached: Project.ProjectSettings.BsnesUsageMapImportOnlyChangedUnmarked
            );
            linesModified = importer.Run();
            
            // 2. to clean it up a little, run our "fixup" stuff.
            FixMisalignedFlags();
            RescanForInOut();

        }, "Import usage map + fixup flags + rescan IN/Out");
        
        if (linesModified > 0)
            MarkChanged();

        return linesModified;
    }

    public long ImportBsnesTraceLogs(string[] fileNames)
    {
        var importer = new BsnesTraceLogImporter(Project.Data.GetSnesApi());

        // TODO: differentiate between binary-formatted and text-formatted files
        // probably look for a newline within 80 characters
        // call importer.ImportTraceLogLineBinary()

        var largeFilesReader = controllerFactory.GetLargeFileReaderProgressController();

        // caution: trace logs can be gigantic, even a few seconds can be > 1GB
        // inside here, performance becomes critical.
        largeFilesReader.Filenames = new List<string>(fileNames);
        largeFilesReader.LineReadCallback = line => importer.ImportTraceLogLine(line);
        largeFilesReader.Run();

        if (importer.CurrentStats.NumRomBytesModified > 0)
            MarkChanged();

        return importer.CurrentStats.NumRomBytesModified;
    }

    public long ImportBsnesTraceLogsBinary(IEnumerable<string> filenames, BsnesTraceLogCaptureController.TraceLogCaptureSettings workItemCaptureSettings)
    {
        var importer = new BsnesTraceLogImporter(Project.Data.GetSnesApi());

        foreach (var file in filenames)
        {
            using Stream source = File.OpenRead(file);
            const int bytesPerPacket = 22;
            var buffer = new byte[bytesPerPacket];
            int bytesRead;
            while ((bytesRead = source.Read(buffer, 0, bytesPerPacket)) > 0)
            {
                Debug.Assert(bytesRead == 22);
                importer.ImportTraceLogLineBinary(buffer, true, workItemCaptureSettings);
            }
        }
        
        importer.CopyTempGeneratedCommentsIntoMainSnesData();

        return importer.CurrentStats.NumRomBytesModified;
    }
        
    public void CloseProject()
    {
        if (Project == null)
            return;

        ProjectChanged?.Invoke(this, new IProjectController.ProjectChangedEventArgs
        {
            ChangeType = IProjectController.ProjectChangedEventArgs.ProjectChangedType.Closing
        });

        Project = null;
    }

    /// <summary>
    /// Confirm with user that the project export settings are valid, then start exporting.
    /// </summary>
    /// <returns>True if we exported assembly, false if we didn't / aborted.</returns>
    public bool ConfirmSettingsThenExportAssembly()
    {
        var newlyEditedSettings = ShowSettingsEditorUntilValid();
        return WriteAssemblyOutputIfSettingsValid(newlyEditedSettings);
    }

    /// <summary>
    /// Export assembly using current project settings (fails if settings not currently valid) 
    /// </summary>
    /// <returns>True if we exported assembly, false if we didn't / aborted.</returns>
    public bool ExportAssemblyWithCurrentSettings() => 
        WriteAssemblyOutputIfSettingsValid() || ConfirmSettingsThenExportAssembly();

    [CanBeNull]
    public LogWriterSettings ShowSettingsEditorUntilValid()
    {
        LogWriterSettings newlyEditedSettings = null;

        do
        {
            var shouldAskUserToContinue = newlyEditedSettings != null; 
            if (shouldAskUserToContinue && !PromptUserTryAgainOrAbortExport())
                return null;

            newlyEditedSettings = ShowExportSettingsEditor();
            if (newlyEditedSettings == null)
                return null;
                
        } while (!newlyEditedSettings.IsValid(fs));

        return newlyEditedSettings;
    }

    private bool PromptUserTryAgainOrAbortExport() => 
        commonGui.PromptToConfirmAction("Can't export assembly because export settings are invalid. Edit now?");

    public bool WriteAssemblyOutputIfSettingsValid() => 
        WriteAssemblyOutputIfSettingsValid(Project?.LogWriterSettings);

    public bool WriteAssemblyOutputIfSettingsValid(LogWriterSettings settingsToUseAndSave)
    {
        if (settingsToUseAndSave == null || !settingsToUseAndSave.IsValid(fs))
            return false;
        
        // must have saved the project first
        if (Project.Session?.ProjectDirectory.Length == 0)
            return false;

        UpdateExportSettings(settingsToUseAndSave);
        WriteAssemblyOutput();
        
        return true;
    }

    [CanBeNull]
    private LogWriterSettings ShowExportSettingsEditor()
    {
        var exportSettingsController = CreateExportSettingsEditorController();
        return !(exportSettingsController?.PromptSetupAndValidateExportSettings() ?? false) 
            ? null 
            : exportSettingsController.Settings;
    }
    
    [CanBeNull]
    private ILogCreatorSettingsEditorController CreateExportSettingsEditorController()
    {
        if (Project == null)
            return null;
        
        var exportSettingsController = controllerFactory.GetAssemblyExporterSettingsController();
        exportSettingsController.KeepPathsRelativeToThisPath = Project.Session?.ProjectDirectory ?? "";
        exportSettingsController.Settings = Project.LogWriterSettings with { }; // operate on a new copy of the settings
        return exportSettingsController;
    }
}