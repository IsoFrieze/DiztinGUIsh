using System.ComponentModel;
using System.Runtime.CompilerServices;
using Diz.Core.Interfaces;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Cpu._65816.import;

public interface ISnesRomAnalyzerData : INotifyPropertyChanged
{
    public record SnesRomAnalysisResults(
        bool DetectedRomMapModeCorrectly, 
        RomMapMode RomMapMode,
        RomSpeed RomSpeed
    );
    
    public string? Filename { get; }
    public IReadOnlyList<byte>? RomBytes { get; }
    public SnesRomAnalysisResults? AnalysisResults { get; }
    public int? RomSettingsOffset { get; }
}

public interface ISnesRomAnalyzer : ISnesRomAnalyzerData
{
    public void Analyze(string romFilename);
    public void Analyze(IReadOnlyList<byte> romBytes, string? romFilename = null);
}


public class SnesRomAnalyzer : ISnesRomAnalyzer
{
    public string? Filename
    {
        get => filename;
        private set => this.SetField(PropertyChanged, ref filename, value);
    }
    private string? filename;

    public IReadOnlyList<byte>? RomBytes
    {
        get => romBytes;
        private set => this.SetField(PropertyChanged, ref romBytes, value);
    }
    private IReadOnlyList<byte>? romBytes;

    public ISnesRomAnalyzer.SnesRomAnalysisResults? AnalysisResults
    {
        get => analysisResults;
        private set => this.SetField(PropertyChanged, ref analysisResults, value);
    }
    private ISnesRomAnalyzer.SnesRomAnalysisResults? analysisResults;

    public int? RomSettingsOffset =>
        AnalysisResults != null 
            ? RomUtil.GetRomSettingOffset(AnalysisResults.RomMapMode) 
            : null;

    public void Reset()
    {
        Filename = null;
        AnalysisResults = null;
        RomBytes = null;
    }

    public void Analyze(string romFilename)
    {
        Reset();

        var rawRomBytes = RomUtil.ReadRomFileBytes(romFilename);
        Analyze(rawRomBytes);

        Filename = romFilename;
    }

    public void Analyze(IReadOnlyList<byte> rawRomBytes, string? romFilename = null)
    {
        Reset();
        AnalysisResults = DetectSettingsFor(rawRomBytes);
        RomBytes = rawRomBytes;
        Filename = romFilename;
    }

    private static ISnesRomAnalyzer.SnesRomAnalysisResults DetectSettingsFor(IReadOnlyList<byte> romBytes)
    {
        var romMapMode = RomUtil.DetectRomMapMode(romBytes, out var detectedRomMapModeCorrectly);
        
        return new ISnesRomAnalyzer.SnesRomAnalysisResults(
            DetectedRomMapModeCorrectly: detectedRomMapModeCorrectly, 
            RomMapMode: romMapMode, 
            RomSpeed: RomUtil.GetRomSpeed(romMapMode, romBytes)
        );
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [NotifyPropertyChangedInvocator]
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}