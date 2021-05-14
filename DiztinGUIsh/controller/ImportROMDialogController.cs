using System.ComponentModel;
using System.Diagnostics;
using Diz.Core.serialization;
using Diz.Core.util;

namespace DiztinGUIsh.controller
{
    public interface IImportRomDialogView
    {
        bool PromptToConfirmAction(string msg);
        bool ShowAndWaitForUserToConfirmSettings();
        ImportRomDialogController Controller { get; set; }
        bool GetVectorValue(int i, int j);
        void RefreshUi();
    }

    public class ImportRomDialogController
    {
        public IImportRomDialogView View { get; set; }
        public ImportRomSettingsBuilder Builder { get; private set; }

        public delegate void SettingsCreatedEvent();
        public event SettingsCreatedEvent OnBuilderInitialized;

        public ImportRomSettings PromptUserForImportOptions(string romFilename)
        {
            Builder = new ImportRomSettingsBuilder(romFilename);
            return !PromptUserForOptions()
                ? null 
                : Builder.CreateSettings();
        }

        private bool PromptUserForOptions()
        {
            Debug.Assert(Builder != null);
            
            OnBuilderInitialized?.Invoke();
            Builder.ImportSettings.PropertyChanged += ImportSettingsOnPropertyChanged;
            Refresh();

            return View.ShowAndWaitForUserToConfirmSettings();
        }

        private void ImportSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Refresh();
        }

        private void Refresh()
        {
            View?.RefreshUi();
            UpdateVectorsFromView();
        }

        private void UpdateVectorsFromView()
        {
            Builder.VectorTableEntriesEnabled = ImportRomSettingsBuilder.GenerateVectors(
                (i, j) => View?.GetVectorValue(i, j) ?? true
            );
        }

        private bool Warn(string msg)
        {
            return View.PromptToConfirmAction(msg +
                                              "\nIf you proceed with this import, imported data might be wrong.\n" +
                                              "Proceed anyway?\n\n (Experts only, otherwise say No)");
        }

        public bool Submit()
        {
            if (!Builder.DetectedMapMode.HasValue)
            {
                if (!Warn("ROM Map type couldn't be detected."))
                    return false;
            }
            else if (Builder.DetectedMapMode.Value != Builder.ImportSettings.RomMapMode)
            {
                if (!Warn("The ROM map type selected is different than what was detected."))
                    return false;
            }

            return true;
        }
    }
}
