using Diz.Controllers.interfaces;
using Diz.Core.serialization;
using Diz.Cpu._65816.import;

namespace Diz.Controllers.controllers;

public interface IImportRomDialogController
{
    IImportRomDialogView View { get; set; }
    public ISnesRomImportSettingsBuilder Builder { get; }
    public event SettingsCreatedEvent OnBuilderInitialized;
        
    string CartridgeTitle { get; }
    string RomSpeedText { get; }

    public ImportRomSettings PromptUserForImportOptions(string romFilename);
        
    public delegate void SettingsCreatedEvent();

    public bool Submit();
    int GetVectorTableValue(int whichTable, int whichEntry);
    bool IsProbablyValidDetection();
    string GetDetectionMessage();
}