using System.Linq;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Core.model;

public static class LabelProviderExtensions
{
    // Normalize any labels in WRAM into their canonical addresses.
    // i.e. a label at $001234 (a WRAM mirror) moves to $7E1234 (the canonical WRAM address).
    //
    // Moved verbatim from SnesData.NormalizeWramLabels() (Diz.Cpu.65816): the operation only
    // touches the label provider and RomUtil, both Diz.Core, so it lives here where UI-free
    // consumers (e.g. Diz.Ui.ViewModels) can reach it without referencing the CPU assembly.
    // SnesData.NormalizeWramLabels() remains as a thin delegation to this.
    public static void NormalizeWramLabels(this ILabelProvider labels)
    {
        var wramLabels = labels.Labels
            .Where(x => RomUtil.GetWramAddressFromSnesAddress(x.Key) != -1)
            .ToList();

        foreach (var label in wramLabels)
        {
            var normalizedSnesAddress = RomUtil.GetSnesAddressFromWramAddress(RomUtil.GetWramAddressFromSnesAddress(label.Key));

            // already normalized? skip
            if (normalizedSnesAddress == label.Key)
                continue;

            // if there are duplicates or overlaps, we can't proceed, they must be manually cleaned up
            if (wramLabels.Any(x => x.Key == normalizedSnesAddress))
                continue;

            labels.RemoveLabel(label.Key);
            labels.AddLabel(normalizedSnesAddress, label.Value, true);
        }
    }
}
