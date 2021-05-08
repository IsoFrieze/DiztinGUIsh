using System;
using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.serialization;

namespace Diz.Core.util
{
    public class ImportRomSettingsBuilder
    {
        public ImportRomSettings ImportSettings { get; set; }
        public RomMapMode? DetectedMapMode { get; set; }
        public Dictionary<string, bool> VectorTableEntriesEnabled { get; set; }
        public bool ShouldCheckHeader { get; set; } = true;
        
        public static ImportRomSettingsBuilder CreateFor(string romFilename)
        {
            var builder = new ImportRomSettingsBuilder();
            builder.Init(romFilename);
            return builder;
        }

        public void Init(string romFilename)
        {
            ImportSettings = new ImportRomSettings
            {
                RomFilename = romFilename,
                RomBytes = RomUtil.ReadRomFileBytes(romFilename),
                RomMapMode =
                    RomUtil.DetectRomMapMode(RomUtil.ReadRomFileBytes(romFilename), out var detectedMapModeSuccess)
            };

            if (detectedMapModeSuccess)
                DetectedMapMode = ImportSettings.RomMapMode;

            VectorTableEntriesEnabled = GenerateVectors();
        }

        public ImportRomSettings CreateSettings()
        {
            ImportSettings.InitialLabels = RomUtil.GenerateVectorLabels(
                VectorTableEntriesEnabled, ImportSettings.RomSettingsOffset, ImportSettings.RomBytes,
                ImportSettings.RomMapMode);

            ImportSettings.InitialHeaderFlags =
                !ShouldCheckHeader
                    ? new Dictionary<int, Data.FlagType>()
                    : RomUtil.GenerateHeaderFlags(ImportSettings.RomSettingsOffset, ImportSettings.RomBytes);

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
            var importSettings = ImportRomSettingsBuilder.CreateFor(romFilename).CreateSettings();
            return ImportRomAndCreateNewProject(importSettings);
        }
        
        public static Project ImportRomAndCreateNewProject(ImportRomSettings importSettings)
        {
            var project = new Project
            {
                AttachedRomFilename = importSettings.RomFilename,
                ProjectFileName = null,
                Data = new Data()
            };

            project.Data.RomMapMode = importSettings.RomMapMode;
            project.Data.RomSpeed = importSettings.RomSpeed;
            project.Data.CreateRomBytesFromRom(importSettings.RomBytes);

            foreach (var pair in importSettings.InitialLabels)
                project.Data.AddLabel(pair.Key, pair.Value, true);

            foreach (var pair in importSettings.InitialHeaderFlags)
                project.Data.SetFlag(pair.Key, pair.Value);

            project.CacheVerificationInfo();
            project.UnsavedChanges = true;

            return project;
        }
    }
}