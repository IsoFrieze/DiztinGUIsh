using System;
using System.Diagnostics;
using System.IO;
using Diz.Core.util;

namespace Diz.Core.serialization.xml_serializer
{
    // this isn't the only place migrations can happen.
    // preconditions:
    // - Integrity checks run and version numbers are compatible for migrations
    // - any XML-based migrations (from ExtendedXmlSerializer etc) have already run
    public static class PostSerializeMigrations
    {
        public static void Run(ref AddRomDataCommand romAddCmd, bool preRomLoad)
        {
            var effectiveVersion = romAddCmd?.Root?.SaveVersion;
            if (effectiveVersion == null)
                throw new InvalidOperationException("Can't read a valid effective version from project save file");
            
            if (effectiveVersion <= 100)
            {
                MigrateVersion100To101(ref romAddCmd, preRomLoad);
                effectiveVersion = 101;
            }

            Debug.Assert(effectiveVersion == ProjectXmlSerializer.CurrentSaveFormatVersion);
        }

        private static void MigrateVersion100To101(ref AddRomDataCommand romAddCmd, bool preRomLoad)
        { 
            MitigateBug_050_JapaneseText(ref romAddCmd, preRomLoad);
        }

        // works around bug with Japanese text in Cart Title in the SNES header
        // https://github.com/Dotsarecool/DiztinGUIsh/issues/50
        private static void MitigateBug_050_JapaneseText(ref AddRomDataCommand romAddCmd, bool preRomLoad)
        {
            if (preRomLoad)
            {
                // this will have the loader skip checking the cart title name.
                // we'll down our own check later.
                romAddCmd.ShouldProjectCartTitleMatchRomBytes = false;
                return;
            }
            
            // we're called now after the romBytes have been loaded and all other checks are clear.

            romAddCmd.ShouldProjectCartTitleMatchRomBytes = true;

            // check for japanese character encoding bug in game title.
            // this is a result of us not storing XML correctly in earlier version of Diz, meaning we can't rely on
            // this bit of data in the project file to serve as an integrity check.  it's OK though because 
            // checksums are unaffected, so as long as they match, we should be ok to proceed.

            var project = romAddCmd?.Root?.Project;
            if (project == null)
                return;
            
            var cartridgeTitleFromRom = project.InternalRomGameName; // this is the buggy string we're working around
            var deserializedRomCartridgeTitle = project.Data.CartridgeTitleName; // this should always be correct now that the bug is fixed
            var mismatchedGameTitleInXmlVsRom = deserializedRomCartridgeTitle != cartridgeTitleFromRom;

            if (!mismatchedGameTitleInXmlVsRom)
            {
                // we're not experiencing the bug, everything is now good.
                return;
            }

            // if the checksums match, but the internal ROM title doesn't, we'll assume we hit this bug and
            // reset the project cartridge name from the actual bytes in the ROM.

            // we're going a little overkill on checking here to make sure everything's good.
            // NOTE: we're not checking for a VALID checksum, only that our various checksums match each other.
            var checksumXmlVsBytesMatch = project.InternalCheckSum == project.Data.RomCheckSumsFromRomBytes;
            var checksumXmlVsCalculatedMatch = project.Data.ComputeChecksum() == project.Data.RomChecksum;

            var allChecksumsGood = checksumXmlVsBytesMatch && checksumXmlVsCalculatedMatch;
            if (!allChecksumsGood)
                throw new InvalidDataException("Migration to save file format version 101: Rom checksums from project and rom bytes don't match. Can't continue.");
            
            // ok, assume we hit the bug. to fix the broken save data, we will now
            // re-cache the verification info. it will be stored correctly on the next serialize.
            project.CacheVerificationInfo();
        }
    }
}