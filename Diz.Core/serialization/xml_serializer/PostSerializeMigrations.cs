using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.serialization.xml_serializer
{
    public interface IMigrationEvents
    {
        void OnLoadingBeforeAddLinkedRom(AddRomDataCommand romAddCmd);
        void OnLoadingAfterAddLinkedRom(AddRomDataCommand romAddCmd);
    }
    
    public interface IMigration : IMigrationEvents
    {
        // Each Migration has a unique version#, and will upgrade data in that version#
        // to the next version#.
        public int AppliesToSaveVersion { get; }
    }

    public interface IMigrationRunner : IMigrationEvents
    {
        
    }
    
    // this isn't the only place migrations can happen.
    // preconditions:
    // - Integrity checks run and version numbers are compatible for migrations
    // - any XML-based migrations (from ExtendedXmlSerializer etc) have already run
    public class MigrationRunner : IMigrationRunner
    {
        // every item in this list must have it's target version >= the previous version (sorted)
        // multiple of the same version# ARE allowed.
        // items will be applied in the order they're in the list.
        public List<IMigration> Migrations { get; } = new List<IMigration>();
        
        public int StartingSaveVersion { get; set; }
        public int TargetSaveVersion { get; set; }

        private IEnumerable<IMigration> CreateQueue()
        {
            var afterMigrationTheVersionShouldBe = StartingSaveVersion;

            var itemsToActuallyRun = new Queue<IMigration>();
            foreach (var item in Migrations)
            {
                // this item has the same version as the previous version, this is ok,
                // but walk back the version number we're on
                if (item.AppliesToSaveVersion == afterMigrationTheVersionShouldBe - 1)
                    afterMigrationTheVersionShouldBe--; // undo

                if (item.AppliesToSaveVersion > afterMigrationTheVersionShouldBe)
                    throw new InvalidDataException($"internal: couldn't find migration for version# {afterMigrationTheVersionShouldBe}");

                // process all items, but, only allow adding into the queue ones that are in our range
                if (item.AppliesToSaveVersion >= StartingSaveVersion && item.AppliesToSaveVersion <= TargetSaveVersion)
                    itemsToActuallyRun.Enqueue(item);

                Debug.Assert(item.AppliesToSaveVersion == afterMigrationTheVersionShouldBe);
                afterMigrationTheVersionShouldBe++;
            }

            if (afterMigrationTheVersionShouldBe != TargetSaveVersion)
                throw new InvalidDataException(
                    "internal: migration seuquence doesn't reach desired target version number");

            return itemsToActuallyRun;
        }
        
        private void RunAllMigrations(Action<IMigration> applyAction)
        {
            var currentVersion = StartingSaveVersion;
            
            foreach (var migration in CreateQueue())
            {
                if (migration.AppliesToSaveVersion == currentVersion - 1)
                    currentVersion--;
                
                Debug.Assert(migration.AppliesToSaveVersion == currentVersion);
                
                applyAction(migration);
                currentVersion++;
            }

            Debug.Assert(currentVersion == TargetSaveVersion);
        }

        public void OnLoadingBeforeAddLinkedRom(AddRomDataCommand romAddCmd)
        {
            RunAllMigrations(migration => migration.OnLoadingBeforeAddLinkedRom(romAddCmd));
        }

        public void OnLoadingAfterAddLinkedRom(AddRomDataCommand romAddCmd)
        {
            RunAllMigrations(migration => migration.OnLoadingAfterAddLinkedRom(romAddCmd));
        }
    }

    // check for japanese character encoding bug in game title.
    // this is a result of us not storing XML correctly in earlier version of Diz, meaning we can't rely on
    // this bit of data in the project file to serve as an integrity check.  it's OK though because 
    // checksums are unaffected, so as long as they match, we should able to rely on that to complete our 
    // validation checks.
    // https://github.com/Dotsarecool/DiztinGUIsh/issues/50
    public class MigrationBugfix050JapaneseText : IMigration
    {
        public int AppliesToSaveVersion { get; } = 100; 
        
        private bool previousCartTitleMatchState;
        private bool beforeAddRun;

        public virtual void OnLoadingBeforeAddLinkedRom(AddRomDataCommand romAddCmd)
        {
            // this will have the loader skip checking the cart title name.
            // we'll down our own check later.
            previousCartTitleMatchState = romAddCmd.ShouldProjectCartTitleMatchRomBytes; 
            romAddCmd.ShouldProjectCartTitleMatchRomBytes = false;

            beforeAddRun = true;
        }

        public virtual void OnLoadingAfterAddLinkedRom(AddRomDataCommand romAddCmd)
        {
            var project = Setup(romAddCmd);

            if (!IsMitigationNeeded(project)) 
                return;

            ApplyMitigation(project);
        }

        private Project Setup(AddRomDataCommand romAddCmd)
        {
            Debug.Assert(beforeAddRun);

            // we're called now after the romBytes have been loaded and all other checks are clear.
            romAddCmd.ShouldProjectCartTitleMatchRomBytes = previousCartTitleMatchState;

            return romAddCmd.Root?.Project;
        }

        private static void ApplyMitigation(Project project)
        {
            // if the checksums match, but the internal ROM title doesn't, we'll assume we hit this bug and
            // reset the project cartridge name from the actual bytes in the ROM.

            // we're going a little overkill on checking here to make sure everything's good.
            // NOTE: we're not checking for a VALID checksum, only that our various checksums match each other.
            var checksumXmlVsBytesMatch = project.InternalCheckSum == project.Data.RomCheckSumsFromRomBytes;
            var checksumXmlVsCalculatedMatch = project.Data.ComputeChecksum() == project.Data.RomChecksum;

            var allChecksumsGood = checksumXmlVsBytesMatch && checksumXmlVsCalculatedMatch;
            if (!allChecksumsGood)
                throw new InvalidDataException(
                    "Migration to save file format version 101: Rom checksums from project and rom bytes don't match. Can't continue.");

            // ok, assume we hit the bug. to fix the broken save data, we will now
            // re-cache the verification info. it will be stored correctly on the next serialize.
            project.CacheVerificationInfo();
        }

        private static bool IsMitigationNeeded(Project project)
        {
            var cartridgeTitleFromRom = project.InternalRomGameName; // this won't be correct if we hit the bug
            var deserializedRomCartridgeTitle =
                project.Data.CartridgeTitleName; // this will be correct, even if we hit the bug
            
            // we need to mitigate the bug if our titles don't agree with each other.
            return deserializedRomCartridgeTitle != cartridgeTitleFromRom;
        }
    }
}