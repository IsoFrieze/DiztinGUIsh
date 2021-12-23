#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Diz.Controllers.interfaces;
using Diz.Core.export;
using Diz.Core.util;
using Diz.LogWriter;
using Diz.LogWriter.util;
using JetBrains.Annotations;

namespace Diz.Controllers.controllers;

public class LogCreatorSettingsEditorController : ILogCreatorSettingsEditorController
{
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
    
    private LogWriterSettings settings = null!;
    private string? keepPathsRelativeToThisPath;

    public LogCreatorSettingsEditorController(ILogCreatorSettingsEditorView view, LogWriterSettings startingSettings)
    {
        View = view;
        View.Controller = this;
        View.Closed += OnClosed;
        
        Settings = startingSettings;
        UseDefaultsIfInvalidSettings();
    }
    
    public bool PromptSetupAndValidateExportSettings() => 
        View.PromptEditAndConfirmSettings() && Settings.IsValid();

    public event EventHandler? Closed;

    public ILogCreatorSettingsEditorView View { get; set; }
    
    public string GetSampleOutput()
    {
        try
        {
            return LogUtil.GetSampleAssemblyOutput(Settings).OutputStr;
        }
        catch (Exception ex)
        {
            return $"Invalid format or sample output: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [NotifyPropertyChangedInvocator]
    public void OnPropertyChanged([CallerMemberName] string? propertyName = null) => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void UseDefaultsIfInvalidSettings() => 
        Settings = Settings.GetDefaultsIfInvalid();
    
    public bool ValidateFormat(string formatStr) => 
        LogCreatorLineFormatter.Validate(formatStr);
    
    public bool EnsureSelectRealOutputDirectory(bool forcePrompt = false)
    {
        var result = PromptToCreateOutputDirIfNeeded("Press YES to create and use this path, NO to select a new path instead.");

        if (result == ILogCreatorSettingsEditorController.PromptCreateDirResult.DidCreateItNow)
            return true;

        var shouldPrompt = forcePrompt || result == ILogCreatorSettingsEditorController.PromptCreateDirResult.DontWantToCreateItNow;
        if (shouldPrompt && !PromptForPath())
            return false;
        
        return PromptToCreateOutputDirIfNeeded() is 
            ILogCreatorSettingsEditorController.PromptCreateDirResult.AlreadyExists or 
            ILogCreatorSettingsEditorController.PromptCreateDirResult.DidCreateItNow;
    }

    /// <summary>
    /// If the output directory doesn't exist, ask the user if they'd like to create it. 
    /// </summary>
    /// <returns>true if the directory exists (either already existing or was just created), false if user chose not to create it</returns>
    private ILogCreatorSettingsEditorController.PromptCreateDirResult PromptToCreateOutputDirIfNeeded(string extraMsg = "")
    {
        if (DoesOutputDirExist())
            return ILogCreatorSettingsEditorController.PromptCreateDirResult.AlreadyExists; // already exists, so we're good.

        // doesn't exist, ask if they want to create it now
        if (!View.PromptCreatePath(Settings.BuildFullOutputPath(), extraMsg))
            return ILogCreatorSettingsEditorController.PromptCreateDirResult.DontWantToCreateItNow; // they don't want to create it now
            
        // yes, they want to create the directory now
        CreateOutputDirIfNeeded();
        return ILogCreatorSettingsEditorController.PromptCreateDirResult.DidCreateItNow;
    }

    private string? GetOutputDirectoryName() => 
        Path.GetDirectoryName(Settings.BuildFullOutputPath());
        
    private bool DoesOutputDirExist()
    {
        var outputDirectoryName = GetOutputDirectoryName();
        return Directory.Exists(outputDirectoryName);
    }

    private void CreateOutputDirIfNeeded()
    {
        if (DoesOutputDirExist())
            return;

        // TODO: catch exceptions here.
        var outputDirectoryName = GetOutputDirectoryName();
        if (!string.IsNullOrEmpty(outputDirectoryName))
            Directory.CreateDirectory(outputDirectoryName);
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

    private void OnClosed(object? sender, EventArgs eventArgs) => 
        Closed?.Invoke(sender, eventArgs);
}