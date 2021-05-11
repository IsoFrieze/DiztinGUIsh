using System;
using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.serialization;
using JetBrains.Annotations;

namespace Diz.Core.util
{
    public class ImportRomSettingsBuilder
    {
        public ImportRomSettings ImportSettings { get; set; }
        public RomMapMode? DetectedMapMode { get; set; }
        public Dictionary<string, bool> VectorTableEntriesEnabled { get; set; }
        public bool ShouldCheckHeader { get; set; } = true;
        
        [PublicAPI] 
        public ImportRomSettingsBuilder()
        {
            
        }
        
        [PublicAPI]
        public ImportRomSettingsBuilder(string romFilenameToAnalyze)
        {
            Init(romFilenameToAnalyze);
        }

        public void Init(string romFilename)
        {
            var rawRomBytes = RomUtil.ReadRomFileBytes(romFilename);
            
            ImportSettings = new ImportRomSettings
            {
                RomFilename = romFilename,
                RomBytes = rawRomBytes,
                RomMapMode = RomUtil.DetectRomMapMode(rawRomBytes, out var detectedMapModeSuccess),
            };

            ImportSettings.RomSpeed = RomUtil.GetRomSpeed(ImportSettings.RomSettingsOffset, rawRomBytes);

            if (detectedMapModeSuccess)
                DetectedMapMode = ImportSettings.RomMapMode;

            VectorTableEntriesEnabled = GenerateVectors();
        }

        public ImportRomSettings CreateSettings()
        {
            ImportSettings.InitialLabels = RomUtil.GenerateVectorLabels(
                VectorTableEntriesEnabled, ImportSettings.RomSettingsOffset, ImportSettings.RomBytes, ImportSettings.RomMapMode);

            if (ShouldCheckHeader)
                ImportSettings.InitialHeaderFlags =
                    RomUtil.GenerateHeaderFlags(ImportSettings.RomSettingsOffset, ImportSettings.RomBytes);

            return ImportSettings;
        }

        // stored like this to match the layout of the checkboxes on the importer GUI form
        // TODO: this is not a great way to do this, our data shouldn't be dependent on the GUI. should be other way around.
        public static readonly string[,] VectorNames = {
            {"Native_COP", "Native_BRK", "Native_ABORT", "Native_NMI", "Native_RESET", "Native_IRQ"},
            {"Emulation_COP", "Emulation_Unknown", "Emulation_ABORT", "Emulation_NMI", "Emulation_RESET", "Emulation_IRQBRK"}
        };

        public static string GetVectorName(int i, int j) => VectorNames[i, j];

        public static Dictionary<string, bool> GenerateVectors(Func<int, int, bool> getValue = null)
        {
            var newVectors = new Dictionary<string, bool>();
            for (var i = 0; i < VectorNames.GetLength(0); i++)
            {
                for (var j = 0; j < VectorNames.GetLength(1); j++)
                {
                    newVectors.Add(GetVectorName(i, j), getValue?.Invoke(i, j) ?? true);
                }
            }

            return newVectors;
        }
    }

    public static class ImportUtils
    {
        public static Project ImportRomAndCreateNewProject(string romFilename)
        {
            // automated headless helper method to use all default settings and pray it works
            // no GUI or anything. use with caution, only if you know what you're doing
            return ImportRomAndCreateNewProject(
                new ImportRomSettingsBuilder(romFilename)
                    .CreateSettings());
        }

        public static Project ImportRomAndCreateNewProject(ImportRomSettings importSettings)
        {
            var project = new Project
            {
                AttachedRomFilename = importSettings.RomFilename,
                ProjectFileName = null,
                Data = new Data()
            };
            
            project.Data.PopulateFrom(importSettings.RomBytes, importSettings.RomMapMode, importSettings.RomSpeed);

            foreach (var (offset, label) in importSettings.InitialLabels)
                project.Data.Labels.AddLabel(offset, label, true);

            foreach (var (offset, flagType) in importSettings.InitialHeaderFlags)
                project.Data.SetFlag(offset, flagType);

            project.CacheVerificationInfo();
            project.UnsavedChanges = true;

            return project;
        }
    }
}
