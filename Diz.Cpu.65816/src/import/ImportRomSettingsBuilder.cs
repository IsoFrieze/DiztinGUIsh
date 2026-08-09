#nullable enable

using System.ComponentModel;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Core.model.snes;
using Diz.Core.serialization;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Cpu._65816.import;

[UsedImplicitly]
public class SnesRomImportSettingsBuilder : ISnesRomImportSettingsBuilder
{
    private bool optionGenerateHeaderFlags = true;
    private RomMapMode optionSelectedRomMapMode;
    private bool optionGenerateSelectedVectorTableLabels = true;
    private bool optionGenerateBankRegions = true;
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
        set
        {
            if (!this.SetField(PropertyChanged, ref optionSelectedRomMapMode, value))
                return;

            // the vector table sits at a different ROM offset under each mapping, so the cached
            // entries describe the OLD mapping until they're rebuilt. Everything GenerateSettings()
            // derives from them would otherwise point at the wrong bytes.
            RegenerateCachedVectorTableEntries();
        }
    }

    public bool OptionGenerateSelectedVectorTableLabels
    {
        get => optionGenerateSelectedVectorTableLabels;
        set => this.SetField(PropertyChanged, ref optionGenerateSelectedVectorTableLabels, value);
    }

    public bool OptionGenerateBankRegions
    {
        get => optionGenerateBankRegions;
        set => this.SetField(PropertyChanged, ref optionGenerateBankRegions, value);
    }

    // Where the ROM settings header (and therefore the vector table) lives, AT THE MAPPING THE
    // USER SELECTED -- not the one detection guessed at. Detection only seeds the selection; once
    // the user overrules it, every offset derived from it has to move too, or the import produces
    // a project tagged with one mapping and labelled/flagged using another's offsets.
    // Null until the ROM has been analysed at all, and null when the selected mapping puts that
    // header outside the file -- a small ROM read as HiROM has no header to find, and everything
    // downstream would otherwise read past the end of it.
    private int? RomSettingOffset
    {
        get
        {
            if (Input.AnalysisResults == null || Input.RomBytes == null)
                return null;

            var offset = RomUtil.GetRomSettingOffset(OptionSelectedRomMapMode);
            return offset > 0 && offset <= Input.RomBytes.Count ? offset : null;
        }
    }

    // ALL vector table entries (native and emulation) for the currently selected Rom Map Mode
    // (including unused/deselected/etc)
    private IVectorTableCache VectorTableForCurrentMapMode { get; }
    
    // a list of enabled vector table entries, varies with the UI.
    private List<string> EnabledVectorEntries { get; } = [];

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
        OnAnalyzed();
    }

    public void Analyze(byte[] rawRomBytes)
    {
        Reset();
        Input.Analyze(rawRomBytes);
        OnAnalyzed();
    }

    private void OnAnalyzed()
    {
        SetRomMapModeToAnalyzed();
    }

    private void SetRomMapModeToAnalyzed()
    {
        OptionSelectedRomMapMode = Input.AnalysisResults?.RomMapMode ?? RomMapMode.LoRom;
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
            RomMapMode = OptionSelectedRomMapMode,
            RomSpeed = Input.AnalysisResults.RomSpeed,
            InitialLabels = OptionGenerateSelectedVectorTableLabels ? GenerateVectorLabels() : new Dictionary<int, Label>()
        };

        if (OptionGenerateHeaderFlags)
            settings.InitialHeaderFlags =
                RomUtil.GenerateHeaderFlags(RomSettingOffset ?? -1, Input.RomBytes);

        if (OptionGenerateBankRegions)
            settings.InitialRegions = GenerateBankRegions();

        return settings;
    }

    // on a brand-new import there are no existing regions to reconcile against, so this
    // synthesizes one whole-bank region per bank in the
    // ROM. Uses the same shared helper as the save-format-107 migration and the LogWriter's
    // export-time synthesis, so all three call sites agree on bank extents/skip rules.
    private List<IRegion> GenerateBankRegions()
    {
        var romMapMode = OptionSelectedRomMapMode;
        var romSpeed = Input.AnalysisResults!.RomSpeed;
        var bankSize = RomUtil.GetBankSize(romMapMode);

        return BankRegionSynthesis.SynthesizeMissingBankRegions(
                existingRegions: [],
                romSize: Input.RomBytes!.Count,
                bankSize: bankSize,
                convertPcToSnes: offset => RomUtil.ConvertPCtoSnes(offset, romMapMode, romSpeed))
            .Cast<IRegion>()
            .ToList();
    }

    private Dictionary<int, Label> GenerateVectorLabels()
    {
        var allEntries = VectorTableForCurrentMapMode.Entries ?? [];

        return EnabledVectorEntries
            .Select(x => allEntries.Single(entry => entry.VectorTableEntry.Name == x))
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
        var (romOffset, vectorTableEntry) = entry;

        return new KeyValuePair<int, Label>(romOffset, new Label {
            Name = vectorTableEntry.Name,
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}