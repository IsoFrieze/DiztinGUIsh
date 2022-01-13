using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.util;
using Diz.Cpu._65816.import;

namespace Diz.Controllers.controllers
{
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
                
                return RomUtil.GetRomSettingOffset(Builder.Input.AnalysisResults.RomMapMode);
            }
        }

        public IReadOnlyList<byte> RomBytes => 
            Builder.Input.RomBytes;

        public RomSpeed RomSpeed =>
            Builder.Input.AnalysisResults?.RomSpeed ?? RomSpeed.Unknown;

        public RomMapMode RomMapMode => 
            Builder.Input.AnalysisResults?.RomMapMode ?? default;

        public string CartridgeTitle => 
            RomUtil.GetCartridgeTitleFromRom(RomBytes, RomSettingsOffset);

        public string RomSpeedText => 
            Util.GetEnumDescription(RomSpeed);
        
        public string GetDetectionMessage() =>
            Builder.Input.AnalysisResults is { DetectedRomMapModeCorrectly: true }
                ? RomMapModeText
                : "Couldn't auto detect ROM Map Mode!";

        public string RomMapModeText => 
            Util.GetEnumDescription(RomMapMode);

        public ImportRomDialogController(ICommonGui commonGui, IImportRomDialogView view, ISnesRomImportSettingsBuilder builder)
        {
            this.commonGui = commonGui;
            Builder = builder;
            
            View = view;
        }

        public ImportRomSettings PromptUserForImportOptions(string romFilename)
        {
            return !PromptUserForOptions()
                ? null 
                : Builder.GenerateSettings();
        }

        private bool PromptUserForOptions()
        {
            Debug.Assert(Builder != null);
            
            OnBuilderInitialized?.Invoke();
            Builder.PropertyChanged += ImportSettingsOnPropertyChanged;
            Refresh();

            var result = View.ShowAndWaitForUserToConfirmSettings();
            
            Builder.PropertyChanged -= ImportSettingsOnPropertyChanged;
            View = null;
            
            return result;
        }
        
        private bool IsOffsetInRange(int offset) => 
            Builder.Input.RomBytes != null && offset > 0 && offset <= Builder.Input.RomBytes.Count;

        public bool IsProbablyValidDetection() =>
            Builder.Input.AnalysisResults != null &&
            Builder.Input.AnalysisResults.RomSpeed != RomSpeed.Unknown &&
            IsOffsetInRange(Builder.Input.RomSettingsOffset ?? -1);

        private void ImportSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e) => 
            Refresh();

        private void Refresh()
        {
            View?.RefreshUi();
            SyncVectorTableEntriesFromGui();
        }

        private void SyncVectorTableEntriesFromGui()
        {
            Builder.OptionClearGenerateVectorTableLabels();
            foreach (var vectorEntry in View.GetEnabledVectorTableEntries())
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
            else if (analysisResults.RomMapMode != Builder.GenerateSettings().RomMapMode)
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
}
