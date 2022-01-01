#nullable enable

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Diz.Controllers.interfaces;
using Diz.Core.export;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Controllers.controllers;

[UsedImplicitly]
public class LogCreatorSettingsEditorController : ILogCreatorSettingsEditorController
{
    private enum PromptCreateDirResult
    {
        AlreadyExists,
        DontWantToCreateItNow,
        DidCreateItNow,
    }
    
    public LogWriterSettings Settings
    {
        get => settings;
        set => this.SetField(PropertyChanged, ref settings, value);
    }

    public string? KeepPathsRelativeToThisPath
    {
        get => keepPathsRelativeToThisPath;
        set => this.SetField(PropertyChanged, ref keepPathsRelativeToThisPath, value);
    }

    public ILogCreatorSettingsEditorView View { get; set; }
    
    public event EventHandler? Closed;
    public event PropertyChangedEventHandler? PropertyChanged;
    
    private LogWriterSettings settings = new();
    private string? keepPathsRelativeToThisPath;
    private readonly IFilesystemService fs;

    public LogCreatorSettingsEditorController(ILogCreatorSettingsEditorView view, IFilesystemService fs)
    {
        this.fs = fs;
        Debug.Assert(fs != null);
        
        View = view;
        View.Controller = this;
        View.Closed += OnClosed;
    }
    
    /// <summary>
    /// Show settings editor UI for user to edit.
    /// NOTE: edited settings don't have to be valid when this returns.
    /// </summary>
    /// <returns>True if settings were saved, false if user cancelled the operation and we should discard edits</returns>
    public bool PromptSetupAndValidateExportSettings() => 
        View.PromptEditAndConfirmSettings();
    
    [NotifyPropertyChangedInvocator]
    public void OnPropertyChanged([CallerMemberName] string? propertyName = null) => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public bool EnsureSelectRealOutputDirectory(bool forcePrompt = false)
    {
        var result = PromptToCreateOutputDirIfNeeded("Press YES to create and use this path, NO to select a new path instead.");

        if (result == PromptCreateDirResult.DidCreateItNow)
            return true;

        var shouldPrompt = forcePrompt || result == PromptCreateDirResult.DontWantToCreateItNow;
        if (shouldPrompt && !PromptForPath())
            return false;
        
        return PromptToCreateOutputDirIfNeeded() is 
            PromptCreateDirResult.AlreadyExists or 
            PromptCreateDirResult.DidCreateItNow;
    }

    /// <summary>
    /// If the output directory doesn't exist, ask the user if they'd like to create it. 
    /// </summary>
    /// <returns>true if the directory exists (either already existing or was just created), false if user chose not to create it</returns>
    private PromptCreateDirResult PromptToCreateOutputDirIfNeeded(string extraMsg = "")
    {
        if (DoesOutputDirExist())
            return PromptCreateDirResult.AlreadyExists; // already exists, so we're good.

        // doesn't exist, ask if they want to create it now
        if (!View.PromptCreatePath(Settings.BuildFullOutputPath(), extraMsg))
            return PromptCreateDirResult.DontWantToCreateItNow; // they don't want to create it now
            
        // yes, they want to create the directory now
        CreateOutputDirIfNeeded();
        return PromptCreateDirResult.DidCreateItNow;
    }

    private void CreateOutputDirIfNeeded()
    {
        if (DoesOutputDirExist())
            return;

        // TODO: catch exceptions here.
        var outputDirectoryName = GetOutputDirectoryName();
        if (!string.IsNullOrEmpty(outputDirectoryName)) 
            fs.CreateDirectory(outputDirectoryName);
    }

    private bool PromptForPath()
    {
        var askForFile = Settings.Structure == LogWriterSettings.FormatStructure.SingleFile;
        var selectedFileOrFolderOutPath = View.PromptForLogPathFromFileOrFolderDialog(askForFile);

        if (string.IsNullOrEmpty(selectedFileOrFolderOutPath))
            return false;

        Settings = Settings.WithPathRelativeTo(selectedFileOrFolderOutPath, KeepPathsRelativeToThisPath);

        return true;
    }
    
    private string? GetOutputDirectoryName() => 
        Path.GetDirectoryName(Settings.BuildFullOutputPath());

    private bool DoesOutputDirExist() => 
        fs.DirectoryExists(GetOutputDirectoryName());

    private void OnClosed(object? sender, EventArgs eventArgs) => 
        Closed?.Invoke(sender, eventArgs);
}