#nullable enable

using System.Collections.Immutable;
using System.ComponentModel;
using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Core.serialization;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Cpu._65816.import;

[UsedImplicitly]
public class SnesRomImportSettingsBuilder : ISnesRomImportSettingsBuilder
{
    private bool optionGenerateHeaderFlags;
    private RomMapMode optionSelectedRomMapMode;
    private bool optionGenerateSelectedVectorTableLabels;
    private readonly IReadFromFileBytes fileReader; 

    public ISnesRomAnalyzer Input { get; }

    ISnesRomAnalyzerData ISnesRomImportSettingsBuilder.Input => Input;

    public bool OptionGenerateHeaderFlags
    {
        get => optionGenerateHeaderFlags;
        set => this.SetField(PropertyChanged, ref optionGenerateHeaderFlags, value);
    }

    public RomMapMode OptionSelectedRomMapMode
    {
        get => optionSelectedRomMapMode;
        set => this.SetField(PropertyChanged, ref optionSelectedRomMapMode, value);
    }

    public bool OptionGenerateSelectedVectorTableLabels
    {
        get => optionGenerateSelectedVectorTableLabels;
        set => this.SetField(PropertyChanged, ref optionGenerateSelectedVectorTableLabels, value);
    }

    private int? RomSettingOffset => 
        Input.AnalysisResults == null ? null : RomUtil.GetRomSettingOffset(Input.AnalysisResults.RomMapMode);

    // ALL vector table entries for the currently selected Rom Map Mode
    private IVectorTableCache VectorTableForCurrentMapMode { get; }
    
    // a list of enabled vector table entries, varies with the UI.
    public List<string> EnabledVectorEntries { get; } = new();

    public SnesRomImportSettingsBuilder(ISnesRomAnalyzer snesRomAnalyzer, IVectorTableCache vectorTableCache, IReadFromFileBytes fileReader)
    {
        Input = snesRomAnalyzer;
        VectorTableForCurrentMapMode = vectorTableCache;
        this.fileReader = fileReader;

        Input.PropertyChanged += InputOnPropertyChanged;
        
        Reset();
    }

    private void InputOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Reset();
        RegenerateCachedVectorTableEntries();
    }

    private void RegenerateCachedVectorTableEntries()
    {
        var romSettingsOffset = RomSettingOffset;
        if (!romSettingsOffset.HasValue)
        {
            VectorTableForCurrentMapMode.Clear();
            return;
        }
        
        VectorTableForCurrentMapMode.RegenerateEntriesFor(romSettingsOffset.Value);
    }

    public void Reset()
    {
        OptionGenerateHeaderFlags = true;
    }

    public void Analyze(string romFilename)
    {
        Reset();
        var rawRomBytes = fileReader.ReadRomFileBytes(romFilename);
        Input.Analyze(rawRomBytes, romFilename);
    }

    public void Analyze(byte[] rawRomBytes)
    {
        Reset();
        Input.Analyze(rawRomBytes);
    }

    public void OptionClearGenerateVectorTableLabels()
    {
        EnabledVectorEntries.Clear();
    }

    public void OptionSetGenerateVectorTableLabelFor(string vectorName, bool shouldGenerateLabel)
    {
        var exists = EnabledVectorEntries.Contains(vectorName);

        switch (shouldGenerateLabel)
        {
            case true when !exists:
                EnabledVectorEntries.Add(vectorName);
                break;
            case false when exists:
                EnabledVectorEntries.Remove(vectorName);
                break;
        }
    }

    public ImportRomSettings GenerateSettings()
    {
        if (Input.AnalysisResults == null || Input.RomBytes == null)
            throw new InvalidOperationException("Can't create settings when analysis hasn't taken place yet");

        var settings = new ImportRomSettings
        {
            RomFilename = Input.Filename,
            RomBytes = Input.RomBytes.ToList(),
            RomMapMode = Input.AnalysisResults.RomMapMode,
            InitialLabels = GenerateVectorLabels()
            
            // InitialLabels = // OBSOLETE RomUtil.GenerateVectorLabels(VectorTableEntriesEnabled, RomBytes, AnalysisResults.RomMapMode) // old, now replaced?
        };

        if (OptionGenerateHeaderFlags)
            settings.InitialHeaderFlags =
                RomUtil.GenerateHeaderFlags(RomSettingOffset ?? -1, Input.RomBytes);

        return settings;
    }

    private Dictionary<int, Label> GenerateVectorLabels()
    {
        var allEntries = VectorTableForCurrentMapMode
            .Entries ?? new List<CpuVectorTable.VectorRomEntry>();

        return EnabledVectorEntries
            .Select(x => allEntries.Single(entry => entry.Entry.Name == x))
            .Select(CreateLabelForVectorEntry)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private KeyValuePair<int, Label>? CreateLabelForVectorEntry(CpuVectorTable.VectorRomEntry entry)
    {
        if (!RomSettingOffset.HasValue)
            return null;

        // note: can also do a SNES address here if we wanted to. benefits to doing both.
        // when mirroring works in labels, this will be useful to have both
        var (romOffset, vectorTableEntry, _) = entry;

        return new KeyValuePair<int, Label>(romOffset, new Label {
            Name = vectorTableEntry.Name,
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}