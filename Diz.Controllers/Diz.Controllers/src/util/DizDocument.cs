#nullable enable
using System.ComponentModel;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Controllers.util;

/// <summary>
/// This class is for GUI internal use only, it should never be serialized.
/// It represents the current "open document" (usually a thin wrapper around a Project)
/// Anything that should persist should go inside Project instead.
///
/// This can store some per-user settings that get saved locally.
/// Don't save anything important here though.
/// </summary>
[UsedImplicitly]
public class DizDocument : IDizDocument
{
    private readonly IDizAppSettings appSettings;
    
    public Project? Project
    {
        get => project;
        set => this.SetField(PropertyChanged, ref project, value, compareRefOnly: true);
    }
    private Project? project;

    public string LastProjectFilename
    {
        get => appSettings.LastProjectFilename;
        set
        {
            var projectName = appSettings.LastProjectFilename;
            this.SetField(PropertyChanged, ref projectName, value);
            appSettings.LastProjectFilename = projectName;
        }
    }

    public BindingList<NavigationEntry> NavigationHistory
    {
        get => navigationHistory;
        set => this.SetField(PropertyChanged, ref navigationHistory, value);
    }

    private BindingList<NavigationEntry> navigationHistory = new()
    {
        RaiseListChangedEvents = true,
        AllowNew = false,
        AllowRemove = false,
        AllowEdit = false,
    };

    public DizDocument(IDizAppSettings appSettings)
    {
        this.appSettings = appSettings;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}