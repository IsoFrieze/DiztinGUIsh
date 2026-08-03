using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Diz.Controllers.importers;
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
using Diz.Ui.ViewModels.ExportSettings;
using JetBrains.Annotations;

namespace Diz.Controllers.controllers;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class ProjectController(
    ICommonGui commonGui,
    IFilesystemService fs,
    IControllerFactory controllerFactory,
    IViewFactory viewFactory,
    Func<LogWriterSettings, ISampleAssemblyTextGenerator> sampleAssemblyTextGeneratorCreate,
    Func<RomImporterRegistry> romImporterRegistryCreate,
    Func<ImportRomSettings, IProjectFactoryFromRomImportSettings> projectImporterFactoryCreate,
    Func<IProjectFileManager> projectFileManagerCreate)
    : IProjectController
{
    public IProjectView ProjectView { get; set; }
    public Project Project { get; private set; }

    public event IProjectController.ProjectChangedEvent ProjectChanged;

    // new-ui plan step 6: the long-running-task contract is now Task-based. The work runs
    // off the UI thread via the view's TaskHandler (which shows a progress window and marshals
    // progress); callers `await` and only read results after the returned Task completes -- the
    // captured-local-plus-blocking anti-pattern is gone. When there is no UI (headless export,
    // unit tests: TaskHandler == null), the work runs inline and synchronously, so the returned
    // Task is already completed and needs no message pump.
    private sealed class NoProgress : IProgress<int> { public void Report(int value) { } }
    private static readonly IProgress<int> NullProgress = new NoProgress();

    public Task DoLongRunningTaskAsync(Action task, string description = null) =>
        DoLongRunningTaskAsync((_, _) => task(), description, isMarquee: true);

    public async Task DoLongRunningTaskAsync(
        Action<IProgress<int>, CancellationToken> work, string description, bool isMarquee)
    {
        var handler = ProjectView?.TaskHandler;
        if (handler == null)
        {
            // headless / test fallback: run inline, no UI, no message pump.
            work(NullProgress, CancellationToken.None);
            return;
        }

        await handler(work, description, isMarquee);
    }

    public async Task<bool> OpenProjectAsync(string filename)
    {
        Diz.Core.util.StartupTrace.Log($"ProjectController.OpenProjectAsync: opening {filename}");
        ProjectOpenResult projectOpenResult = null;
        var errorMsg = "";

        await DoLongRunningTaskAsync(delegate
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

    public async Task<string> SaveProjectAsync(string filename)
    {
        try
        {
            var emptyFilename = string.IsNullOrEmpty(filename);
            if (emptyFilename)
                throw new ArgumentException("empty filename specified", nameof(filename));

            string err = null;
            await DoLongRunningTaskAsync(
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

    /// <summary>
    /// Shown when the file cannot be matched to any console Diz knows how to import. Unreachable
    /// while one importer is registered -- the registry sends everything to it -- and the reason
    /// this path exists at all is so that adding a second one produces a question for the user
    /// rather than a silent guess at which console a file belongs to.
    /// </summary>
    public const string UnrecognizedRomFormatMessage =
        "Couldn't work out what kind of ROM this is. Try a file with a recognized extension.";

    // a fresh registry (and so a fresh importer) per import: an importer drives a settings builder
    // that holds the analysed ROM, and that state belongs to one import and no other.
    public async Task<bool> ImportRomAndCreateNewProjectAsync(string romFilename)
    {
        var importer = romImporterRegistryCreate().SelectFor(romFilename);
        if (importer == null)
        {
            commonGui.ShowError(UnrecognizedRomFormatMessage);
            return false;
        }

        var importSettings = await importer.ChooseImportSettingsAsync(romFilename);
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

    // step 4 of the new-ui plan: takes a plain path (the view layer prompts via
    // IFileDialogService) instead of taking the view and calling dialogs back on it.
    public void ImportLabelsCsv(string importFilename, bool replaceAll)
    {
        if (string.IsNullOrEmpty(importFilename))
            return;

        var errLine = 0;
        try
        {
            Project.Data.Labels.ImportLabelsFromCsv(importFilename, replaceAll, smartMerge: true, out errLine);
            // note: no view callback here anymore. the VM-bound label editor (step 3)
            // re-syncs itself from the provider's LabelsChanged events, which is what the
            // old labelEditor.RepopulateFromData() call did manually.
        }
        catch (Exception ex)
        {
            // same user-visible dialog the old labelEditor.ShowLineItemError produced
            // (WinformsGuiUtil.ShowLineItemError): identical text, title, icon, button.
            // errLine is 0 unless ImportLabelsFromCsv assigned it before throwing, which
            // preserves the historical (quirky) "no line number shown" behavior for
            // exceptions thrown mid-parse.
            commonGui.ShowError(
                "An error occurred while parsing the file.\n" + ex.Message +
                (errLine > 0 ? $" (Check line {errLine}.)" : ""));
        }
    }
    
    private string AskToSelectNewRomFilename(string error) => 
        ProjectView.AskToSelectNewRomFilename("Error", $"{error}\n\nLink a new ROM now?");

    public Task WriteAssemblyOutputAsync()
    {
        return WriteAssemblyOutputAsync(Project.LogWriterSettings, true);
    }

    private async Task WriteAssemblyOutputAsync(LogWriterSettings settings, bool showProgressBarUpdates = false)
    {
        var lc = new LogCreator
        {
            Settings = settings,
            Data = new LogCreatorByteSource(Project.Data),
        };

        LogCreatorOutput.OutputResult result = null;
        await DoLongRunningTaskAsync(() => result = lc.CreateLog(), "Exporting assembly source code...");

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

    public void NormalizeWramLabels()
    {
        if (!commonGui.PromptToConfirmAction(
                "This converts all WRAM labels (where possible and non-overlapping) to the $7E/$7F range. Proceed?"))
            return;
        
        Project.Data.GetSnesApi()?.NormalizeWramLabels();
    }
    
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

    public async Task<long> ImportBsnesUsageMapAsync(string fileName)
    {
        var snesData = Project?.Data.GetSnesApi();
        if (snesData == null)
            return 0;

        var linesModified = 0;
        await DoLongRunningTaskAsync(() =>
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

    public async Task<long> ImportBsnesTraceLogsAsync(string[] fileNames)
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

        // determinate progress (bytes read / total): the reader reports 0..100 via IProgress.
        await DoLongRunningTaskAsync(
            (progress, token) => largeFilesReader.Read(progress, token),
            "Importing trace logs...", isMarquee: false);

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
            ChangeType = IProjectController.ProjectChangedEventArgs.ProjectChangedType.Closing,
        });

        Project = null;
    }

    /// <summary>
    /// Confirm with user that the project export settings are valid, then start exporting.
    /// </summary>
    /// <returns>True if we exported assembly, false if we didn't / aborted.</returns>
    public async Task<bool> ConfirmSettingsThenExportAssemblyAsync()
    {
        var newlyEditedSettings = await ShowSettingsEditorUntilValidAsync();
        return await WriteAssemblyOutputIfSettingsValidAsync(newlyEditedSettings);
    }

    /// <summary>
    /// Export assembly using current project settings (fails if settings not currently valid)
    /// </summary>
    /// <returns>True if we exported assembly, false if we didn't / aborted.</returns>
    public async Task<bool> ExportAssemblyWithCurrentSettingsAsync() =>
        await WriteAssemblyOutputIfSettingsValidAsync() || await ConfirmSettingsThenExportAssemblyAsync();

    /// <summary>
    /// Show the export-settings screen until what the user chose can actually be exported, or they
    /// give up. Returns the edited settings, or null if they cancelled.
    ///
    /// THE SCREEN IS BUILT ONCE AND RE-SHOWN. Re-seeding it from the project on every retry --
    /// which is what used to happen -- discards everything typed so far the moment the user answers
    /// "yes, edit now", i.e. exactly when they are being asked to fix a typo in what they typed.
    /// </summary>
    [CanBeNull]
    public async Task<LogWriterSettings> ShowSettingsEditorUntilValidAsync()
    {
        var viewModel = CreateExportSettingsViewModel();
        if (viewModel == null)
            return null;

        var firstAttempt = true;

        while (true)
        {
            if (!firstAttempt && !PromptUserTryAgainOrAbortExport())
                return null;

            firstAttempt = false;

            if (!await viewFactory.GetExportSettingsView().EditAsync(viewModel))
                return null;

            var newlyEditedSettings = viewModel.BuildSettings();
            if (newlyEditedSettings.IsValid(fs))
                return newlyEditedSettings;
        }
    }

    private bool PromptUserTryAgainOrAbortExport() => 
        commonGui.PromptToConfirmAction("Can't export assembly because export settings are invalid. Edit now?");

    public Task<bool> WriteAssemblyOutputIfSettingsValidAsync() =>
        WriteAssemblyOutputIfSettingsValidAsync(Project?.LogWriterSettings);

    public async Task<bool> WriteAssemblyOutputIfSettingsValidAsync(LogWriterSettings settingsToUseAndSave)
    {
        if (settingsToUseAndSave == null || !settingsToUseAndSave.IsValid(fs))
            return false;

        // must have saved the project first
        if (Project.Session?.ProjectDirectory.Length == 0)
            return false;

        // save asm exporter settings
        UpdateExportSettings(settingsToUseAndSave);

        // OPTIONAL: save the project file, just in case anything goes wrong during export
        if (Project?.ProjectFileName != "")
            await SaveProjectAsync(Project?.ProjectFileName);

        // do the real output
        await WriteAssemblyOutputAsync();

        return true;
    }

    /// <summary>
    /// Build the export-settings screen's state from the open project, wired to the two things the
    /// screen needs from the assembly writer: whether a line template parses, and what a few lines
    /// of assembly would look like written with the current settings. Both are handed over as
    /// delegates because the ViewModel assembly is not allowed to reference the assembly writer.
    /// </summary>
    [CanBeNull]
    private ExportSettingsViewModel CreateExportSettingsViewModel()
    {
        if (Project == null)
            return null;

        // a copy: nothing on screen touches the project until the export actually runs.
        return new ExportSettingsViewModel(
            Project.LogWriterSettings with { },
            fs,
            LogCreatorLineFormatter.Validate,
            GenerateSampleAssemblyText);
    }

    /// <summary>
    /// Render the sample-output box's contents. A template can parse and still blow up while being
    /// used, so a failure comes back as the text to show rather than as an exception -- the screen
    /// has always reported it this way.
    /// </summary>
    private string GenerateSampleAssemblyText(LogWriterSettings settings)
    {
        try
        {
            return sampleAssemblyTextGeneratorCreate(settings).GetSampleAssemblyOutput().AssemblyOutputStr;
        }
        catch (Exception ex)
        {
            return $"Invalid format or sample output: {ex.Message}";
        }
    }
}