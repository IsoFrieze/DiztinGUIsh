#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Diz.Controllers.controllers;
using Diz.Core;
using Diz.Core.commands;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Cpu._65816;

// NOTE: lots of these interfaces were created temporarily for major refactoring.
// when that process is finished, we should probably take a pass here to simplify anything
// that ended up being unnecessary or overcomplicated

namespace Diz.Controllers.interfaces;

public interface IProjectController :
    IDataUtilities
{
    // trace log importers
    void ImportBizHawkCdl(string filename);
    Task<long> ImportBsnesUsageMapAsync(string fileName);
    Task<long> ImportBsnesTraceLogsAsync(string[] fileNames);

    // fix instruction utils
    // probably combine this with something else.
    bool RescanForInOut();

    // diz3.0 is going to need some major surgery from this one.

    public Project Project { get; }
        
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
        public Project? Project;
        public string Filename = "";
    }
                
    delegate void ProjectChangedEvent(object sender, ProjectChangedEventArgs e);
    event ProjectChangedEvent ProjectChanged;

    IProjectView ProjectView { get; set; }

    Task<bool> OpenProjectAsync(string filename);
    Task<string> SaveProjectAsync(string filename); // null on success, else the error message

    Task<bool> ImportRomAndCreateNewProjectAsync(string romFilename);
    // path is supplied by the caller (obtained via IFileDialogService in the view layer);
    // this method never prompts. errors are surfaced through ICommonGui.
    void ImportLabelsCsv(string importFilename, bool replaceAll);
    void SelectOffset(int offset, ISnesNavigation.HistoryArgs? historyArgs = null);

    Task<bool> ConfirmSettingsThenExportAssemblyAsync();
    Task<bool> ExportAssemblyWithCurrentSettingsAsync();

    /// <summary>
    /// Let the user edit the export settings until they are exportable, or give up. Returns the
    /// edited settings, or null if they cancelled -- nothing is written and nothing is saved, so a
    /// caller that wants an export must pass the result to
    /// <see cref="WriteAssemblyOutputIfSettingsValidAsync(LogWriterSettings)"/>.
    ///
    /// Split out from <see cref="ConfirmSettingsThenExportAssemblyAsync"/> so a host can keep its
    /// own window on screen while the settings are being edited and hide it only for the export.
    /// </summary>
    Task<LogWriterSettings?> ShowSettingsEditorUntilValidAsync();

    Task<bool> WriteAssemblyOutputIfSettingsValidAsync();
    Task<bool> WriteAssemblyOutputIfSettingsValidAsync(LogWriterSettings? settingsToUseAndSave);
    void MarkChanged(); // rename to MarkUnsaved or similar in Diz3.0
}
    
public interface IProjectOpenerHandler : ILongRunningTaskHandler
{
    public void OnProjectOpenSuccess(string filename, Project project);
    public void OnProjectOpenWarnings(IReadOnlyList<string> warnings);
    public void OnProjectOpenFail(string fatalError);
    public string AskToSelectNewRomFilename(string error);
        
    Project OpenProject(string filename, bool showPopupAlertOnLoaded);
}

public interface IDizAppSettings : INotifyPropertyChanged
{
    string LastProjectFilename { get; set; }
    bool OpenLastFileAutomatically { get; set; }
}

public interface IDizDocument : INotifyPropertyChanged
{
    Project Project { get; set; }
    string LastProjectFilename { get; set; }
    public BindingList<NavigationEntry> NavigationHistory { get; set; }
}