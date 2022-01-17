using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.util;
using Diz.Cpu._65816.import;
using JetBrains.Annotations;

namespace Diz.Controllers.controllers;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class ImportRomDialogController : IImportRomDialogController
{
    public IImportRomDialogView View { get; set; }
    public ISnesRomImportSettingsBuilder Builder { get; }
        
    public event IImportRomDialogController.SettingsCreatedEvent OnBuilderInitialized;
        
    private readonly ICommonGui commonGui;

    public int RomSettingsOffset
    {
        get
        {
            if (Builder.Input.AnalysisResults == null)
                return -1;
                
            return RomUtil.GetRomSettingOffset(Builder.OptionSelectedRomMapMode);
        }
    }

    public IReadOnlyList<byte> RomBytes => 
        Builder.Input.RomBytes;

    public RomSpeed RomSpeed =>
        Builder.Input.AnalysisResults?.RomSpeed ?? RomSpeed.Unknown;

    public string CartridgeTitle => 
        RomUtil.GetCartridgeTitleFromRom(RomBytes, RomSettingsOffset);

    public string RomSpeedText => 
        Util.GetEnumDescription(RomSpeed);
        
    public string GetDetectionMessage()
    {
        string msg = null;
        if (Builder.Input.AnalysisResults is { DetectedRomMapModeCorrectly: true })
            msg = RomMapModeText;
        
        return msg ?? "Couldn't auto detect ROM Map Mode!";
    }

    [CanBeNull]
    public string RomMapModeText
    {
        get
        {
            var romMapModeTxt = Builder?.Input?.AnalysisResults?.RomMapMode;
            return romMapModeTxt != null ? Util.GetEnumDescription(romMapModeTxt) : null;
        }
    }

    public ImportRomDialogController(ICommonGui commonGui, IImportRomDialogView view, ISnesRomImportSettingsBuilder builder)
    {
        this.commonGui = commonGui;
        Builder = builder;
            
        View = view;
    }

    public ImportRomSettings PromptUserForImportOptions(string romFilename)
    {
        return !PromptUserForOptions(romFilename)
            ? null 
            : Builder.GenerateSettings();
    }

    private string romFileNameAnalyzed = "";

    private bool PromptUserForOptions(string romFilename)
    {
        Debug.Assert(Builder != null);
     
        romFileNameAnalyzed = romFilename;
        ReAnalyze();
            
        Builder.PropertyChanged += BuilderOnPropertyChanged;
        OnBuilderInitialized?.Invoke();

        Refresh();
        var result = View.ShowAndWaitForUserToConfirmSettings();
        Refresh();
            
        Builder.PropertyChanged -= BuilderOnPropertyChanged;
        View = null;
            
        return result;
    }

    private void ReAnalyze()
    {
        Builder.Analyze(romFileNameAnalyzed);
    }

    private void BuilderOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        Refresh();
    }

    private bool IsOffsetInRange(int offset) => 
        Builder.Input.RomBytes != null && offset > 0 && offset <= Builder.Input.RomBytes.Count;

    public bool IsProbablyValidDetection() =>
        Builder.Input.AnalysisResults != null &&
        Builder.Input.AnalysisResults.RomSpeed != RomSpeed.Unknown &&
        IsOffsetInRange(Builder.Input.RomSettingsOffset ?? -1);
    
    private void Refresh()
    {
        View?.RefreshUi();
        SyncVectorTableEntriesFromGui();
    }

    private void SyncVectorTableEntriesFromGui()
    {
        Builder.OptionClearGenerateVectorTableLabels();
        foreach (var vectorEntry in View.EnabledVectorTableEntries)
        {
            Builder.OptionSetGenerateVectorTableLabelFor(vectorEntry, true);
        }
    }

    private bool Warn(string msg)
    {
        return commonGui.PromptToConfirmAction(msg +
                                               "\nIf you proceed with this import, imported data might be wrong.\n" +
                                               "Proceed anyway?\n\n (Experts only, otherwise say No)");
    }

    public bool Submit()
    {
        if (Builder == null)
        {
            Warn("Internal error (couldn't build new ROM import settings). Aborting");
            return false;
        }

        var analysisResults = Builder.Input.AnalysisResults;
        if (analysisResults == null)
        {
            Warn("Internal error (Rom analysis results were empty). Aborting");
            return false;
        }
            
        if (!analysisResults.DetectedRomMapModeCorrectly)
        {
            if (!Warn("ROM Map type couldn't be detected."))
                return false;
        }
        else if (analysisResults.RomMapMode != Builder.OptionSelectedRomMapMode)
        {
            if (!Warn("The ROM map type selected is different than what was detected."))
                return false;
        }

        return true;
    }

    public int GetVectorTableValue(int whichTable, int whichEntry)
    {
        Debug.Assert(whichTable is 0 or 1);
        var tableOffset = 0x10 * whichTable;

        Debug.Assert(whichEntry is >= 0 and < 6);
        var vectorEntry = 2 * whichEntry;

        var baseAddr = RomSettingsOffset + 15;
        var romOffset = baseAddr + tableOffset + vectorEntry;

        var vectorValue = RomBytes[romOffset] + (RomBytes[romOffset + 1] << 8);
        return vectorValue;
    }
}