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
/// D1: what the user PICKS is what the import uses.
///
/// The ROM here is the sample ROM padded out to $10000 bytes so that both the LoROM header
/// ($7FD5) and the HiROM header ($FFD5) are inside the file -- otherwise "read this as HiROM"
/// isn't a thing the ROM can express and there is nothing to assert. Detection still says LoROM,
/// because that is where the real header is, so overriding to HiROM is a genuine divergence
/// between detected and selected.
///
/// Before this, every offset the import derived came from the DETECTED mapping while the project
/// was tagged with the SELECTED one, so overriding produced a HiROM project carrying LoROM
/// labels and LoROM header flags.
/// </summary>
public class SnesRomImporterMapModeOverrideTest : ContainerFixture
{
    private const int PaddedRomSize = 0x10000;

    [Inject] private readonly ISampleRomTestData sampleDataFixture = null!;
    [Inject] private readonly ISnesRomImportSettingsBuilder builder = null!;

    private const string RomFilename = "SAMPLEROM";

    private Action<SnesImportRomViewModel>? editViewModel;
    private bool confirmWarning = true;
    private const bool confirmView = true;
    private readonly List<string> warningsAsked = [];

    protected override void Configure(IServiceRegistry serviceRegistry)
    {
        base.Configure(serviceRegistry);

        serviceRegistry.Register<ISampleRomTestData, SampleRomTestDataFixture>(new PerContainerLifetime());

        serviceRegistry.Register<IReadFromFileBytes>(factory =>
        {
            var sampleBytes = factory.GetInstance<ISampleRomTestData>().SampleRomBytes;
            var padded = new byte[PaddedRomSize];
            Array.Copy(sampleBytes, padded, Math.Min(sampleBytes.Length, PaddedRomSize));
            return TestUtil.CreateReadFromFileMock(padded).Object;
        });

    }


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
                editViewModel?.Invoke(viewModel);
                return Task.FromResult(confirmView);
            });

        // the real IViewFactory is a LightInject autofactory over toolkit registrations; here it
        // just hands back the stand-in window above.
        var viewFactory = new Mock<IViewFactory>();
        viewFactory.Setup(x => x.GetSnesImportRomView()).Returns(view.Object);

        var commonGui = new Mock<ICommonGui>();
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

    [Fact]
    public async Task ThePaddedRomStillDetectsAsLoRomSoOverridingItIsARealDivergence()
    {
        SnesImportRomViewModel? seen = null;
        await Run(viewModel => seen = viewModel);

        seen!.DetectedRomMapMode.Should().Be(RomMapMode.LoRom);
        seen.DetectionSucceeded.Should().BeTrue();
        sampleDataFixture.SampleRomBytes.Length.Should().BeLessThan(PaddedRomSize);
    }

    [Fact]
    public async Task OverridingTheMapModeMovesTheVectorLabelsToTheSelectedModesVectorTable()
    {
        var settings = await Run(viewModel =>
        {
            viewModel.SelectedRomMapMode = RomMapMode.HiRom;
            foreach (var row in viewModel.Vectors)
                row.IsEnabled = true;
        });

        settings.Should().NotBeNull();
        settings!.RomMapMode.Should().Be(RomMapMode.HiRom);

        var hiRomVectorTable =
            RomUtil.GetRomSettingOffset(RomMapMode.HiRom) + CpuVectorTable.VectorTableSettingsOffset;
        var loRomVectorTable =
            RomUtil.GetRomSettingOffset(RomMapMode.LoRom) + CpuVectorTable.VectorTableSettingsOffset;

        settings.InitialLabels.Should().NotBeEmpty();
        settings.InitialLabels.Keys.Should().OnlyContain(
            offset => offset >= hiRomVectorTable && offset < hiRomVectorTable + 0x20);
        settings.InitialLabels.Keys.Should().NotContain(loRomVectorTable);
    }

    [Fact]
    public async Task OverridingTheMapModeMovesTheHeaderFlagsToTheSelectedModesHeader()
    {
        var settings = await Run(viewModel => viewModel.SelectedRomMapMode = RomMapMode.HiRom);

        var hiRomSettingOffset = RomUtil.GetRomSettingOffset(RomMapMode.HiRom);
        var loRomSettingOffset = RomUtil.GetRomSettingOffset(RomMapMode.LoRom);

        settings!.InitialHeaderFlags.Should().NotBeEmpty();
        settings.InitialHeaderFlags.Keys.Min().Should().BeGreaterThan(loRomSettingOffset);
        settings.InitialHeaderFlags.Keys.Should().Contain(hiRomSettingOffset);
        settings.InitialHeaderFlags.Keys.Should().NotContain(loRomSettingOffset);
    }

    [Fact]
    public async Task AcceptingDetectionLeavesEverythingOnTheDetectedMapping()
    {
        // the other half of D1: users who don't override must see no change at all.
        var settings = await Run(viewModel =>
        {
            foreach (var row in viewModel.Vectors)
                row.IsEnabled = true;
        });

        settings!.RomMapMode.Should().Be(RomMapMode.LoRom);

        var loRomVectorTable =
            RomUtil.GetRomSettingOffset(RomMapMode.LoRom) + CpuVectorTable.VectorTableSettingsOffset;

        settings.InitialLabels.Keys.Should().OnlyContain(
            offset => offset >= loRomVectorTable && offset < loRomVectorTable + 0x20);
        settings.InitialHeaderFlags.Keys.Should().Contain(RomUtil.GetRomSettingOffset(RomMapMode.LoRom));
        warningsAsked.Should().BeEmpty();
    }

    [Fact]
    public async Task OverridingTheMapModeAsksBeforeImporting()
    {
        await Run(viewModel => viewModel.SelectedRomMapMode = RomMapMode.HiRom);

        warningsAsked.Should().ContainSingle();
        warningsAsked[0].Should().StartWith(SnesImportRomViewModel.OverriddenMapModeConfirmationMessage);
        warningsAsked[0].Should().EndWith(SnesRomImporter.ProceedAnywaySuffix);
    }

    [Fact]
    public async Task DecliningThatQuestionReopensTheWindowRatherThanAbandoningTheImport()
    {
        // the question used to live behind the window's OK button, where answering No left the
        // window open. It is asked after the window closes now, so No has to bring it back.
        confirmWarning = false;

        var showCount = 0;
        var settings = await Run(viewModel =>
        {
            if (++showCount == 1)
                viewModel.SelectedRomMapMode = RomMapMode.HiRom;
            else
                viewModel.SelectedRomMapMode = RomMapMode.LoRom;
        });

        showCount.Should().Be(2, "declining put the user back in the window");
        warningsAsked.Should().ContainSingle("the second pass no longer overrides anything");
        settings.Should().NotBeNull();
        settings!.RomMapMode.Should().Be(RomMapMode.LoRom);
    }
}
