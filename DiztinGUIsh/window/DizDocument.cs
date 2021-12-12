using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Diz.Core.model;
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
    public class DizDocument : DizDataModel
    {
        public Project Project
        {
            get => project;
            set => SetField(ref project, value, compareRefOnly: true);
        }

        public string LastProjectFilename
        {
            get => Settings.Default.LastOpenedFile;
            set
            {
                var lastOpenFile = Settings.Default.LastOpenedFile;
                if (!SetField(ref lastOpenFile, value))
                    return;

                Settings.Default.LastOpenedFile = lastOpenFile;
                Settings.Default.Save();
            }
        }

        public BindingList<NavigationEntry> NavigationHistory
        {
            get => navigationHistory;
            set => SetField(ref navigationHistory, value);
        }
        
        private Project project;

        private BindingList<NavigationEntry> navigationHistory = new()
        {
            RaiseListChangedEvents = true,
            AllowNew = false,
            AllowRemove = false,
            AllowEdit = false,
        };
    }
}