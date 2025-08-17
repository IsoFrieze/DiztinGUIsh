using System.Diagnostics;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using JetBrains.Annotations;

namespace Diz.Cpu._65816.import;

// check for japanese character encoding bug in game title.
// this is a result of us not storing XML correctly in earlier version of Diz, meaning we can't rely on
// this bit of data in the project file to serve as an integrity check.  it's OK though because 
// checksums are unaffected, so as long as they match, we should be able to rely on that to complete our 
// validation checks.
// https://github.com/Dotsarecool/DiztinGUIsh/issues/50
[UsedImplicitly]
public sealed class MigrationBugfix050JapaneseText : IMigration
{
    public int AppliesToSaveVersion => 100;
    private bool previousCartTitleMatchState;
    private bool beforeAddRun;

    public void OnLoadingBeforeAddLinkedRom(IAddRomDataCommand romAddCmd)
    {
        // this will have the loader skip checking the cart title name.
        // we'll down our own check later.
        previousCartTitleMatchState = romAddCmd.ShouldProjectCartTitleMatchRomBytes; 
        romAddCmd.ShouldProjectCartTitleMatchRomBytes = false;

        beforeAddRun = true;
    }

    public void OnLoadingAfterAddLinkedRom(IAddRomDataCommand romAddCmd)
    {
        var project = Setup(romAddCmd);

        if (!IsMitigationNeeded(project)) 
            return;

        ApplyMitigation(project);
    }

    private Project Setup(IAddRomDataCommand romAddCmd)
    {
        Debug.Assert(beforeAddRun);

        // we're called now after the romBytes have been loaded and all other checks are clear.
        romAddCmd.ShouldProjectCartTitleMatchRomBytes = previousCartTitleMatchState;

        return romAddCmd.Root?.Project ?? throw new InvalidOperationException();
    }

    private static void ApplyMitigation(IProject project)
    {
        // if the checksums match, but the internal ROM title doesn't, we'll assume we hit this bug and
        // reset the project cartridge name from the actual bytes in the ROM.

        // we're going a little overkill on checking here to make sure everything's good.
        // NOTE: we're not checking for a VALID checksum, only that our various checksums match each other.
        var snesData = project.Data.GetSnesApi() ?? throw new InvalidDataException("No SNES API for this data during Bugfix050Migration");
        
        var checksumXmlVsBytesMatch = project.InternalCheckSum == snesData.RomCheckSumsFromRomBytes;
        var checksumXmlVsCalculatedMatch = snesData.ComputeChecksum() == snesData.RomChecksum;

        var allChecksumsGood = checksumXmlVsBytesMatch && checksumXmlVsCalculatedMatch;
        if (!allChecksumsGood)
            throw new InvalidDataException(
                "Migration to save file format version 101: Rom checksums from project and rom bytes don't match. Can't continue.");

        // ok, assume we hit the bug. to fix the broken save data, we will now
        // re-cache the verification info. it will be stored correctly on the next serialize.
        snesData.CacheVerificationInfoFor(project);
    }

    private static bool IsMitigationNeeded(IProject project)
    {
        var cartridgeTitleFromRom = project.InternalRomGameName; // this won't be correct if we hit the bug
        var deserializedRomCartridgeTitle =
            project.Data.GetSnesApi()?.CartridgeTitleName; // this will be correct, even if we hit the bug
            
        // we need to mitigate the bug if our titles don't agree with each other.
        return deserializedRomCartridgeTitle != cartridgeTitleFromRom;
    }
}