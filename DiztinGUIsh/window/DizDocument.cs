using System.ComponentModel;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.controller;
using DiztinGUIsh.Properties;

namespace DiztinGUIsh.window
{
    // This class is for GUI internal use only, it should never be serialized.
    // It represents the current "open document" (usually a thin wrapper around a Project)
    // Anything that should persist should go inside Project instead.
    //
    // This can store some per-user settings that get saved locally.
    // Don't save anything important here though.
    public class DizDocument : INotifyPropertyChanged
    {
        public Project Project
        {
            get => project;
            set => this.SetField(PropertyChanged, ref project, value, compareRefOnly: true);
        }

        public string LastProjectFilename
        {
            get => Settings.Default.LastOpenedFile;
            set
            {
                var lastOpenFile = Settings.Default.LastOpenedFile;
                if (!this.SetField(PropertyChanged, ref lastOpenFile, value))
                    return;

                Settings.Default.LastOpenedFile = lastOpenFile;
                Settings.Default.Save();
            }
        }

        public BindingList<NavigationEntry> NavigationHistory
        {
            get => navigationHistory;
            set => this.SetField(PropertyChanged, ref navigationHistory, value);
        }
        
        private Project project;

        private BindingList<NavigationEntry> navigationHistory = new()
        {
            RaiseListChangedEvents = true,
            AllowNew = false,
            AllowRemove = false,
            AllowEdit = false,
        };

        public event PropertyChangedEventHandler PropertyChanged;
    }
}