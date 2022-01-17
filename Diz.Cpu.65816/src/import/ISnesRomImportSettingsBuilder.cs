using System.ComponentModel;
using Diz.Core.model;
using Diz.Core.serialization;
using JetBrains.Annotations;

namespace Diz.Cpu._65816.import;

/// <summary>
/// Analyze a ROM, set some import options.
/// Then generate settings that allow the rom to be imported (i.e. create a new Project from this ROM)
/// </summary>
public interface ISnesRomImportSettingsBuilder : INotifyPropertyChanged
{
    public ISnesRomAnalyzerData Input { get; }
    void Analyze(string romFilename);
    void Analyze(byte[] rawRomBytes);
    
    bool OptionGenerateHeaderFlags { get; set; }
    
    // overrides the detection if necessary, may provide incorrect results
    [UsedImplicitly] RomMapMode OptionSelectedRomMapMode { get; set; }
    
    bool OptionGenerateSelectedVectorTableLabels { get; }
    public void OptionClearGenerateVectorTableLabels();
    public void OptionSetGenerateVectorTableLabelFor(string vectorName, bool shouldGenerateLabel);

    public ImportRomSettings GenerateSettings();
}