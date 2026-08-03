using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Diz.Controllers.importers;
using Diz.Controllers.interfaces;
using Diz.Core.Interfaces;
using Diz.Core.model.project;
using Diz.Core.serialization;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.Cpu._65816.import;
using Diz.Test.Utils;
using Diz.Ui.ViewModels.ImportRom;
using FluentAssertions;
using LightInject;
using Moq;
using Xunit;

namespace Diz.Controllers.Test;

/// <summary>
/// Drives <see cref="SnesRomImporter"/> against the sample ROM with a stand-in for the import
/// window: <see cref="editViewModel"/> is whatever the "user" does while the window is up.
///
/// Replaces the tests that drove the old import-dialog controller. Same ground -- defaults, no
/// labels, two labels, the label-generation switch, and what analysis found -- expressed against
/// the ViewModel the window now edits.
/// </summary>
public class SnesRomImporterTest : ContainerFixture
{
    [Inject] private readonly ISampleRomTestData sampleDataFixture = null!;
    [Inject] private readonly ISnesRomImportSettingsBuilder builder = null!;

    private const string RomFilename = "SAMPLEROM";

    /// <summary>What the user does while the import window is up. Null means "confirm as-is".</summary>
    private Action<SnesImportRomViewModel>? editViewModel;

    /// <summary>Whether the stand-in window is confirmed or cancelled.</summary>
    private bool confirmView = true;

    /// <summary>What the user answers to a "proceed anyway?" prompt.</summary>
    private bool confirmWarning = true;

    private SnesImportRomViewModel? lastViewModel;
    private byte[] romBytesOnDisk = [];
    private readonly List<string> errorsShown = [];
    private readonly List<string> warningsAsked = [];

    protected override void Configure(IServiceRegistry serviceRegistry)
    {
        base.Configure(serviceRegistry);

        serviceRegistry.Register<ISampleRomTestData, SampleRomTestDataFixture>(new PerContainerLifetime());

        serviceRegistry.Register<IReadFromFileBytes>(factory =>
        {
            romBytesOnDisk = WithTwoUsableVectors(factory.GetInstance<ISampleRomTestData>().SampleRomBytes);
            return TestUtil.CreateReadFromFileMock(romBytesOnDisk).Object;
        });

    }


    /// <summary>
    /// Every vector slot in the sample ROM holds a value below $8000, which is not a ROM address
    /// -- so nothing is a candidate for a label and the user is offered no choice at all. Point
    /// two of them at $8123 so there is something to tick.
    /// </summary>
    private static byte[] WithTwoUsableVectors(IReadOnlyList<byte> sampleBytes)
    {
        var bytes = sampleBytes.ToArray();
        var vectorTable = RomUtil.LoromSettingOffset + CpuVectorTable.VectorTableSettingsOffset;

        foreach (var vectorTableOffset in new[] { AbortVectorTableOffset, EmulationResetVectorTableOffset })
        {
            bytes[vectorTable + vectorTableOffset] = 0x23;
            bytes[vectorTable + vectorTableOffset + 1] = 0x81;
        }

        return bytes;
    }

    // positions within the vector table, in bytes: Native_ABORT is slot 4, Emulation_RESET slot 14
    private const int AbortVectorTableOffset = 4 * 2;
    private const int EmulationResetVectorTableOffset = 14 * 2;

    /// <summary>
    /// Build the importer by hand around the injected builder. ContainerFixture runs Configure()
    /// BEFORE the Diz-DLL registration sweep, so a lifetime override registered there does not
    /// stick -- resolving the importer from the container would hand it a DIFFERENT builder than
    /// the one these tests assert on.
    /// </summary>
    private SnesRomImporter MakeImporter()
    {
        var view = new Mock<ISnesImportRomView>();
        view.Setup(x => x.EditAsync(It.IsAny<SnesImportRomViewModel>()))
            .Returns<SnesImportRomViewModel>(viewModel =>
            {
                lastViewModel = viewModel;
                editViewModel?.Invoke(viewModel);
                return Task.FromResult(confirmView);
            });

        // the real IViewFactory is a LightInject autofactory over toolkit registrations; here it
        // just hands back the stand-in window above.
        var viewFactory = new Mock<IViewFactory>();
        viewFactory.Setup(x => x.GetSnesImportRomView()).Returns(view.Object);

        var commonGui = new Mock<ICommonGui>();
        commonGui.Setup(x => x.ShowError(It.IsAny<string>()))
            .Callback<string>(msg => errorsShown.Add(msg));
        commonGui.Setup(x => x.PromptToConfirmAction(It.IsAny<string>()))
            .Callback<string>(msg => warningsAsked.Add(msg))
            .Returns(() => confirmWarning);

        return new SnesRomImporter(builder, viewFactory.Object, commonGui.Object);
    }

    private async Task<ImportRomSettings?> Run(Action<SnesImportRomViewModel>? uiActions = null)
    {
        editViewModel = uiActions;
        return await MakeImporter().ChooseImportSettingsAsync(RomFilename);
    }

    // ------------------------------------------------------------------ ported coverage

    [Fact]
    public async Task Defaults()
    {
        var settings = await Run();

        settings.Should().NotBeNull();
        settings!.RomBytes.Should().BeEquivalentTo(romBytesOnDisk);
        settings.RomFilename.Should().Be(RomFilename);
    }

    [Fact]
    public async Task CancellingTheWindowImportsNothing()
    {
        confirmView = false;
        (await Run()).Should().BeNull();
    }

    [Fact]
    public async Task WithNoLabels()
    {
        // every row the user can reach switched off. The four reserved rows are not reachable and
        // stay on -- see D2 below.
        var settings = await Run(viewModel =>
        {
            foreach (var row in viewModel.Vectors)
                row.IsEnabled = false;
        });

        settings!.InitialLabels.Values.Select(label => label.Name)
            .Should().OnlyContain(name => name.Contains("Reserved"),
                "the user unticked everything they were offered");
    }

    [Fact]
    public async Task WithTwoLabels()
    {
        // whichever two rows this ROM actually offers: a vector whose value is not a ROM address
        // has nothing to label, so the ViewModel refuses to tick it.
        var picked = new List<string>();

        var settings = await Run(viewModel =>
        {
            picked = viewModel.Vectors.Where(row => row.IsSelectable).Take(2)
                .Select(row => row.Name).ToList();

            foreach (var row in viewModel.Vectors)
                row.IsEnabled = picked.Contains(row.Name);
        });

        picked.Should().HaveCount(2);

        var vectorNames = settings!.InitialLabels.Select(x => x.Value.Name).ToList();
        vectorNames.Should().Contain(picked);

        // the two the user picked plus the four always-on reserved slots
        vectorNames.Should().HaveCount(6);
    }

    public static TheoryData<bool> EnableDisableLabelGeneration => new() { true, false };

    [Theory, MemberData(nameof(EnableDisableLabelGeneration))]
    public async Task LabelGenerationDisable(bool labelGenerationEnabled)
    {
        // OptionGenerateSelectedVectorTableLabels has no control on screen (it is redundant with
        // the per-vector boxes), so it is set on the builder directly, as the old test did.
        builder.OptionGenerateSelectedVectorTableLabels = labelGenerationEnabled;

        var settings = await Run(viewModel =>
        {
            builder.OptionGenerateSelectedVectorTableLabels = labelGenerationEnabled;

            var picked = viewModel.Vectors.Where(row => row.IsSelectable).Take(2)
                .Select(row => row.Name).ToList();

            foreach (var row in viewModel.Vectors)
                row.IsEnabled = picked.Contains(row.Name);
        });

        settings!.InitialLabels.Should().HaveCount(labelGenerationEnabled ? 6 : 0);
    }

    [Fact]
    public async Task WhatTheWindowIsShownAboutTheRom()
    {
        await Run();

        lastViewModel.Should().NotBeNull();
        lastViewModel!.CartridgeTitle.Should().Be(sampleDataFixture.Project.InternalRomGameName);
        lastViewModel.DetectedRomMapMode.Should().Be(RomMapMode.LoRom);
        lastViewModel.DetectionSucceeded.Should().BeTrue();
        lastViewModel.RomSpeedText.Should().Be(Util.GetEnumDescription(sampleDataFixture.Project.Data.RomSpeed));
        lastViewModel.RequiresConfirmation.Should().BeFalse("nothing was overridden");

        var input = builder.Input;
        input.Filename.Should().Be(RomFilename);
        input.RomBytes.Should().HaveCountGreaterThan(100);
        input.RomSettingsOffset!.Value.Should().Be(RomUtil.LoromSettingOffset);

        var snesRomAnalysisResults = input.AnalysisResults!;
        snesRomAnalysisResults.RomMapMode.Should().Be(RomMapMode.LoRom);
        snesRomAnalysisResults.DetectedRomMapModeCorrectly.Should().Be(true);
        snesRomAnalysisResults.RomSpeed.Should().Be(sampleDataFixture.Project.Data.RomSpeed);
    }

    [Fact]
    public async Task AllSixteenVectorSlotsAreOfferedInVectorTableOrder()
    {
        await Run();

        lastViewModel!.Vectors.Should().HaveCount(16);
        lastViewModel.Vectors[0].Name.Should().Be(SnesVectorNames.Native_Reserved1__ignored);
        lastViewModel.Vectors[15].Name.Should().Be(SnesVectorNames.Emulation_IRQBRK);
    }

    // ------------------------------------------------------------------ D2: the reserved slots

    [Fact]
    public async Task TheFourReservedVectorLabelsSurviveEverySelectableBoxBeingSwitchedOff()
    {
        // D2 is a PRESERVE. These four slots have no control on screen and are emitted no matter
        // what the user does: they are real 65816 vector-table entries that the SNES never uses,
        // so labelling them documents the table. "Tidying" that away is a behavior change.
        var settings = await Run(viewModel =>
        {
            foreach (var row in viewModel.Vectors)
                row.IsEnabled = false;
        });

        var labelNames = settings!.InitialLabels.Values.Select(label => label.Name).ToList();

        labelNames.Should().Contain(SnesVectorNames.Native_Reserved1__ignored);
        labelNames.Should().Contain(SnesVectorNames.Native_Reserved2__ignored);
        labelNames.Should().Contain(SnesVectorNames.Emulation_Reserved1__ignored);
        labelNames.Should().Contain(SnesVectorNames.Emulation_Reserved2__ignored);
        labelNames.Should().HaveCount(4);
    }
}
