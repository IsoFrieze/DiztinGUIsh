namespace Diz.Ui.ViewModels.ImportRom;

/// <summary>
/// Everything about the 65816 interrupt-vector table that depends on which ROM map mode is
/// currently selected, read out of the ROM in one go.
///
/// A snapshot is produced OUTSIDE this assembly: reading the vector table needs the SNES ROM
/// analyzer, which lives in an assembly the ViewModel layer may not reference. The importer
/// hands <see cref="SnesImportRomViewModel"/> a delegate that turns a map mode into one of
/// these, and the ViewModel treats the result as plain data.
///
/// The cartridge title travels with the vectors because it is read from the same
/// map-mode-dependent ROM settings header: change the map mode and BOTH move.
/// </summary>
/// <param name="CartridgeTitle">
/// The internal cartridge name at this map mode's ROM settings offset, or a placeholder when it
/// can't be read.
/// </param>
/// <param name="VectorsReadable">
/// False when the vector table could not be read at this map mode at all -- e.g. selecting a
/// HiROM mapping on a ROM too small to have a HiROM settings header. Every value in
/// <paramref name="Vectors"/> is then a placeholder.
/// </param>
/// <param name="Vectors">
/// One entry per vector-table slot, in VECTOR TABLE ORDER. All sixteen slots are present,
/// including the ones with no user-facing control, so the ordering can be relied on.
/// </param>
public sealed record SnesVectorSnapshot(
    string CartridgeTitle,
    bool VectorsReadable,
    IReadOnlyList<SnesVectorValue> Vectors)
{
    /// <summary>
    /// Stand-in shown wherever a value could not be read out of the ROM. Same text the original
    /// import window used, so nothing about the display changes.
    /// </summary>
    public const string UnreadablePlaceholder = "????";

    /// <summary>Placeholder cartridge title: as many '?' as the internal name has characters.</summary>
    public const string UnreadableCartridgeTitle = "?????????????????????";

    /// <summary>
    /// A snapshot in which nothing could be read: every supplied vector name gets the
    /// placeholder and is marked unreadable.
    /// </summary>
    public static SnesVectorSnapshot Unreadable(IEnumerable<string> vectorNames) =>
        new(
            UnreadableCartridgeTitle,
            VectorsReadable: false,
            vectorNames.Select(name => new SnesVectorValue(name, UnreadablePlaceholder, false)).ToList());
}

/// <summary>One vector-table slot as read at a particular map mode.</summary>
/// <param name="Name">
/// The vector's canonical name -- the same string that becomes the label name on import, and the
/// key <see cref="SnesImportRomViewModel"/> matches rows on. Not display text.
/// </param>
/// <param name="DisplayValue">
/// The word stored in the slot, already rendered for display, or
/// <see cref="SnesVectorSnapshot.UnreadablePlaceholder"/>.
/// </param>
/// <param name="IsReadable">
/// Whether this slot holds something worth pointing a label at. A vector below $8000 is not a
/// ROM address, so there is nothing to label there even when the read itself succeeded.
/// </param>
public sealed record SnesVectorValue(string Name, string DisplayValue, bool IsReadable);
