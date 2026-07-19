using Diz.Core.model.snes;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using JetBrains.Annotations;

namespace Diz.Cpu._65816.import;

// Save format 106 -> 107.
//
// Purely additive: synthesizes one whole-bank, file-producing region per bank present in the
// ROM (Priority = 0, ExportSeparateFile = true, EndSnesAddress = last byte of the bank,
// inclusive), skipping any bank already covered by an existing user region of the same extent
// -- e.g. a whole-bank region a user drew by hand. Does NOT touch EndSnesAddress on any
// existing region.
// Re-running this synthesis (e.g. via a second migration run, or the LogWriter's export-time
// pass -- see AsmCreationInstructions.GenerateSyntheticBankRegions) is idempotent: the same
// exact-match skip rule applies both places via the shared BankRegionSynthesis helper.
[UsedImplicitly]
public sealed class MigrationAddBankRegions107 : IMigration
{
    public int AppliesToSaveVersion => 106;

    public void OnLoadingAfterAddLinkedRom(IAddRomDataCommand romAddCmd)
    {
        var project = romAddCmd.Root?.Project ?? throw new InvalidOperationException();

        var snesData = project.Data.GetSnesApi()
            ?? throw new InvalidDataException("No SNES API for this data during Bank Regions (107) Migration");

        var synthesized = BankRegionSynthesis.SynthesizeMissingBankRegions(
            existingRegions: project.Data.Regions,
            romSize: snesData.GetRomSize(),
            bankSize: snesData.GetBankSize(),
            convertPcToSnes: snesData.ConvertPCtoSnes);

        foreach (var region in synthesized)
            project.Data.Regions.Add(region);
    }
}
