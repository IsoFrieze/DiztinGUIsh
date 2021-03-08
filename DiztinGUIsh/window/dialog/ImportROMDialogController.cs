using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.util;

namespace DiztinGUIsh.window.dialog
{
    public interface IImportRomDialogView
    {
        bool PromptToConfirmAction(string msg);
        bool ShowAndWaitForUserToConfirmSettings();
        ImportRomDialogController Controller { get; set; }
    }

    public class ImportRomDialogController
    {
        public IImportRomDialogView View { get; set; }
        public ImportRomSettings ImportSettings { get; protected set; }
        public RomMapMode? DetectedMapMode { get; protected set; }
        
        public int RomSettingsOffset { get; set; }= -1;
        public bool ShouldCheckHeader { get; set; } = true;
        public Dictionary<string, bool> VectorTableEntriesEnabled { get; set; } = new Dictionary<string, bool>();

        public delegate void SettingsCreatedEvent();
        public event SettingsCreatedEvent SettingsCreated;

        public ImportRomSettings PromptUserForRomSettings(string romFilename)
        {
            CreateSettingsFromRom(romFilename);
            
            var shouldContinue = View.ShowAndWaitForUserToConfirmSettings();
            if (!shouldContinue)
                return null;

            ImportSettings.InitialLabels = GenerateVectorLabels();
            ImportSettings.InitialHeaderFlags = GenerateHeaderFlags();

            return ImportSettings;
        }

        public void CreateSettingsFromRom(string filename)
        {
            var romBytes = RomUtil.ReadAllRomBytesFromFile(filename);
            ImportSettings = new ImportRomSettings
            {
                RomFilename = filename,
                RomBytes = romBytes,
                RomMapMode = RomUtil.DetectRomMapMode(romBytes, out var detectedMapModeSuccess)
            };

            if (detectedMapModeSuccess)
                DetectedMapMode = ImportSettings.RomMapMode;

            OnSettingsCreated();
        }

        protected virtual void OnSettingsCreated()
        {
            SettingsCreated?.Invoke();
        }

        private bool Warn(string msg)
        {
            return View.PromptToConfirmAction(msg +
                                              "\nIf you proceed with this import, imported data might be wrong.\n" +
                                              "Proceed anyway?\n\n (Experts only, otherwise say No)");
        }

        public bool Submit()
        {
            if (!DetectedMapMode.HasValue)
            {
                if (!Warn("ROM Map type couldn't be detected."))
                    return false;
            }
            else if (DetectedMapMode.Value != ImportSettings.RomMapMode)
            {
                if (!Warn("The ROM map type selected is different than what was detected."))
                    return false;
            }

            return true;
        }

        private Dictionary<int, Label> GenerateVectorLabels() =>
            RomUtil.GenerateVectorLabels(
                VectorTableEntriesEnabled, RomSettingsOffset, ImportSettings.RomBytes, ImportSettings.RomMapMode);

        public Dictionary<int, FlagType> GenerateHeaderFlags()
        {
            var flags = new Dictionary<int, FlagType>();

            if (ShouldCheckHeader)
                RomUtil.GenerateHeaderFlags(RomSettingsOffset, flags, ImportSettings.RomBytes);

            return flags;
        }
    }
}
