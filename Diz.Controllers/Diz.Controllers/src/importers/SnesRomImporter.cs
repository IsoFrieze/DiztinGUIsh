#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Diz.Controllers.interfaces;
using Diz.Core.Interfaces;
using Diz.Core.serialization;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.Cpu._65816.import;
using Diz.Ui.ViewModels.ImportRom;
using JetBrains.Annotations;

namespace Diz.Controllers.importers;

/// <summary>
/// Turns a SNES ROM file into the settings a new project is created from: analyse the ROM, let
/// the user confirm or overrule what analysis found, and hand back the result.
///
/// THE SELECTED MAP MODE IS THE ONE THAT COUNTS. Detection only seeds the choice. Everything
/// derived afterwards -- the vector values on screen, the cartridge title, the generated vector
/// labels' ROM offsets, the header flags -- is read at the mapping the user ended up with, so
/// overruling detection produces a project that is internally consistent rather than one tagged
/// with one mapping and labelled using another's offsets.
///
/// It reads the ROM; it does not draw anything. Which window appears is a per-toolkit
/// registration behind <see cref="ISnesImportRomView"/>, and the questions that come with a risky
/// choice are asked here through <see cref="ICommonGui"/>, not by the window.
/// </summary>
[UsedImplicitly]
public class SnesRomImporter : IRomImporter
{
    public string PlatformName => "SNES";

    /// <summary>
    /// .smc and .sfc are what SNES ROMs are normally called; .swc and .fig are the file extensions
    /// left behind by the Super Wild Card and Pro Fighter copiers, whose 512-byte header the
    /// importer already strips. None of this is required -- see the registry -- it is what makes
    /// the file picker show a ROM without the user switching it to "all files".
    /// </summary>
    public IReadOnlyList<string> FileExtensions { get; } = [".smc", ".sfc", ".swc", ".fig"];

    /// <summary>
    /// Appended to every warning about a choice that may produce a bad import. Verbatim from the
    /// window this replaced -- the wording is the whole reason the question is answerable.
    /// </summary>
    public const string ProceedAnywaySuffix =
        "\nIf you proceed with this import, imported data might be wrong.\n" +
        "Proceed anyway?\n\n (Experts only, otherwise say No)";

    /// <summary>Shown when analysis returned nothing at all -- there is no import to configure.</summary>
    public const string EmptyAnalysisResultsMessage =
        "Internal error (Rom analysis results were empty). Aborting";

    /// <summary>
    /// The vector-table slots the 65816 reserves and the SNES never uses. They are real slots at
    /// real ROM addresses, so labelling them documents the table; the user is not offered a way to
    /// switch them off, and they are emitted even when the ROM could not be analysed.
    /// </summary>
    private static readonly string[] AlwaysEnabledVectorNames =
    [
        SnesVectorNames.Native_Reserved1__ignored,
        SnesVectorNames.Native_Reserved2__ignored,
        SnesVectorNames.Emulation_Reserved1__ignored,
        SnesVectorNames.Emulation_Reserved2__ignored,
    ];

    /// <summary>Every vector-table slot, in vector-table order. Offset 0 is a placeholder: only the names are used.</summary>
    private static readonly string[] AllVectorNames =
        CpuVectorTable.ComputeVectorTableNamesAndOffsets(0)
            .Select(entry => entry.VectorTableEntry.Name)
            .ToArray();

    private readonly ISnesRomImportSettingsBuilder builder;
    private readonly IViewFactory viewFactory;
    private readonly ICommonGui commonGui;

    public SnesRomImporter(ISnesRomImportSettingsBuilder builder, IViewFactory viewFactory, ICommonGui commonGui)
    {
        this.builder = builder;
        this.viewFactory = viewFactory;
        this.commonGui = commonGui;
    }

    /// <param name="romFilename">Path to the ROM file to import.</param>
    /// <returns>The settings to create a project from, or null if the user backed out.</returns>
    public async Task<ImportRomSettings?> ChooseImportSettingsAsync(string romFilename)
    {
        if (!Analyze(romFilename))
            return null;

        var analysisResults = builder.Input.AnalysisResults;
        if (analysisResults == null)
        {
            commonGui.ShowError(EmptyAnalysisResultsMessage);
            return null;
        }

        var viewModel = new SnesImportRomViewModel(
            initialSnapshot: ReadSnapshotAt(builder.OptionSelectedRomMapMode),
            detectedRomMapMode: analysisResults.RomMapMode,
            detectionSucceeded: analysisResults.DetectedRomMapModeCorrectly,
            romSpeedText: RomSpeedText(),
            alwaysEnabledVectorNames: AlwaysEnabledVectorNames,

            // every slot starts ticked; the ViewModel then unticks the ones whose value isn't a
            // ROM address, which is what the old window did on every refresh.
            initiallyEnabledVectorNames: AllVectorNames,
            recomputeForMapMode: ReadSnapshotAt);

        if (!await EditUntilConfirmedAsync(viewModel))
            return null;

        ApplyChoicesToBuilder(viewModel);
        return builder.GenerateSettings();
    }

    /// <summary>
    /// Read and analyse the ROM. False means the file couldn't be used as a ROM at all and the
    /// user has already been told why.
    /// </summary>
    private bool Analyze(string romFilename)
    {
        try
        {
            builder.Analyze(romFilename);
            return true;
        }
        catch (InvalidDataException ex)
        {
            // a file whose size rules it out as a SNES ROM -- too small, or an odd size that is
            // neither headered nor unheadered. Nothing downstream can cope with that, and until
            // now it escaped as an unhandled exception.
            commonGui.ShowError($"Couldn't import this ROM: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Show the window, and put the "are you sure?" question if the choices warrant one. Declining
    /// that question puts the user back in the window rather than abandoning the import, which is
    /// what happened when the question lived behind the window's OK button.
    /// </summary>
    private async Task<bool> EditUntilConfirmedAsync(SnesImportRomViewModel viewModel)
    {
        while (true)
        {
            // one view instance per showing: resolve, edit, discard.
            if (!await viewFactory.GetSnesImportRomView().EditAsync(viewModel))
                return false;

            if (!viewModel.RequiresConfirmation)
                return true;

            if (commonGui.PromptToConfirmAction(viewModel.ConfirmationMessage + ProceedAnywaySuffix))
                return true;
        }
    }

    private void ApplyChoicesToBuilder(SnesImportRomViewModel viewModel)
    {
        // the map mode goes first: the builder rebuilds its cached vector-table offsets off it, and
        // the label names set below are resolved against that cache.
        builder.OptionSelectedRomMapMode = viewModel.SelectedRomMapMode;
        builder.OptionGenerateHeaderFlags = viewModel.GenerateHeaderFlags;
        builder.OptionGenerateBankRegions = viewModel.GenerateBankRegions;

        builder.OptionClearGenerateVectorTableLabels();
        foreach (var vectorName in viewModel.EnabledVectorNames)
            builder.OptionSetGenerateVectorTableLabelFor(vectorName, true);
    }

    /// <summary>
    /// The detected ROM speed, rendered. Placeholder when analysis didn't get far enough to be
    /// worth believing -- the speed is never user-overridable, so there is nothing else to show.
    /// </summary>
    private string RomSpeedText() =>
        IsProbablyValidDetection(builder.OptionSelectedRomMapMode)
            ? Util.GetEnumDescription(builder.Input.AnalysisResults?.RomSpeed ?? RomSpeed.Unknown)
            : SnesVectorSnapshot.UnreadablePlaceholder;

    /// <summary>
    /// Read the whole vector table and the cartridge title as they appear under
    /// <paramref name="mapMode"/>. This is the delegate the ViewModel calls when the user picks a
    /// different mapping, and it is why picking one actually changes what is on screen.
    /// </summary>
    private SnesVectorSnapshot ReadSnapshotAt(RomMapMode mapMode)
    {
        var romBytes = builder.Input.RomBytes;
        if (romBytes == null || !IsProbablyValidDetection(mapMode))
            return SnesVectorSnapshot.Unreadable(AllVectorNames);

        try
        {
            var romSettingOffset = RomUtil.GetRomSettingOffset(mapMode);

            var vectors = CpuVectorTable.ComputeVectorTableNamesAndOffsets(romSettingOffset)
                .Select(entry => ReadVector(romBytes, entry))
                .ToList();

            return new SnesVectorSnapshot(
                RomUtil.GetCartridgeTitleFromRom(romBytes, romSettingOffset),
                VectorsReadable: true,
                vectors);
        }
        catch (Exception)
        {
            // reading past the end of a ROM that is simply too small for this mapping. The window
            // shows placeholders and says why; it is not an error, because trying a mapping on for
            // size is exactly what the picker is for.
            return SnesVectorSnapshot.Unreadable(AllVectorNames);
        }
    }

    private static SnesVectorValue ReadVector(IReadOnlyList<byte> romBytes, CpuVectorTable.VectorRomEntry entry)
    {
        var offset = entry.AbsoluteRomOffset;
        var value = romBytes[offset] + (romBytes[offset + 1] << 8);

        return new SnesVectorValue(
            entry.VectorTableEntry.Name,
            Util.NumberToBaseString(value, Util.NumberBase.Hexadecimal, 4),

            // below $8000 is not a ROM address under any SNES mapping, so there is nothing there
            // worth pointing a label at even though the read itself worked.
            IsReadable: value >= 0x8000);
    }

    /// <summary>
    /// Whether the ROM looks like it can be read at <paramref name="mapMode"/> at all: analysis
    /// produced something, it worked out a speed, and this mapping's settings header is inside the
    /// file. The mapping is a parameter because the answer changes with it -- a small ROM read as
    /// HiROM has no settings header, whatever detection said.
    /// </summary>
    private bool IsProbablyValidDetection(RomMapMode mapMode)
    {
        var romBytes = builder.Input.RomBytes;
        if (romBytes == null || builder.Input.AnalysisResults == null)
            return false;

        if (builder.Input.AnalysisResults.RomSpeed == RomSpeed.Unknown)
            return false;

        var romSettingOffset = RomUtil.GetRomSettingOffset(mapMode);
        return romSettingOffset > 0 && romSettingOffset <= romBytes.Count;
    }
}
