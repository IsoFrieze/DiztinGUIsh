using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Core.serialization;
using Diz.Core.util;
using Diz.Cpu._65816.import;
using Diz.Test.Utils;
using FluentAssertions;
using LightInject;
using Moq;
using Xunit;

namespace Diz.Controllers.Test;

public class ImportRomDialogControllerTest : ContainerFixture
{
    [Inject] private readonly IImportRomDialogController importRomDialogController = null!;
    [Inject] private readonly ISampleRomTestData sampleDataFixture = null!;
    
    public event EventHandler? SimulateViewActions;
    private const string RomFilename = "SAMPLEROM";
    private ImportRomSettings? generatedSettings;
    private Mock<IImportRomDialogView>? mockView = null!;

    protected override void Configure(IServiceRegistry serviceRegistry)
    {
        base.Configure(serviceRegistry);

        serviceRegistry.Register<ISampleRomTestData, SampleRomTestDataFixture>(new PerContainerLifetime());

        serviceRegistry.Register<IReadFromFileBytes>(factory =>
        {
            var mockLinkedRomBytesProvider = TestUtil.CreateReadFromFileMock(
                factory.GetInstance<ISampleRomTestData>().SampleRomBytes
            );
            return mockLinkedRomBytesProvider.Object;
        });

        serviceRegistry.Register(factory => new Mock<ICommonGui>().Object);

        serviceRegistry.Register(factory =>
        {
            mockView = new Mock<IImportRomDialogView>();
            mockView.Setup(x => x.ShowAndWaitForUserToConfirmSettings())
                .Callback(
                    () =>
                    {
                        SimulateViewActions?.Invoke(null, EventArgs.Empty);
                        importRomDialogController.Submit();
                    }).Returns(true);

            mockView.SetupGet(x => x.EnabledVectorTableEntries).Returns(new List<string>());

            return mockView.Object;
        });
    }

    private void Run(Action? uiActions = null)
    {
        if (uiActions != null)
            SimulateViewActions += (sender, args) => uiActions();
        
        generatedSettings = importRomDialogController.PromptUserForImportOptions(RomFilename);
    }

    [Fact]
    public void Defaults()
    {
        Run();
        generatedSettings!.RomBytes.Should().BeEquivalentTo(sampleDataFixture.SampleRomBytes);
        generatedSettings.RomFilename.Should().Be(RomFilename);
    }

    [Fact]
    public void WithNoLabels()
    {
        Run(() => importRomDialogController.Builder.OptionClearGenerateVectorTableLabels());
        generatedSettings!.InitialLabels.Should().BeEmpty("We cleared them in the UI code");
    }

    [Fact]
    public void WithTwoLabels()
    {
        mockView!.SetupGet(x => x.EnabledVectorTableEntries)
            .Returns(new List<string>
            {
                SnesVectorNames.Native_ABORT,
                SnesVectorNames.Emulation_RESET,
            });
        
        Run(() =>
        {
            // importRomDialogController.Builder.OptionSetGenerateVectorTableLabelFor(SnesVectorNames.Native_ABORT, true);
            // importRomDialogController.Builder.OptionSetGenerateVectorTableLabelFor(SnesVectorNames.Emulation_RESET, true);
        });

        var vectorNames = generatedSettings!.InitialLabels.Select(x => x.Value.Name).ToList();
        vectorNames.Should().HaveCount(2);
    }

    [Fact]
    public void ControllerProperties()
    {
        Run();
        importRomDialogController.CartridgeTitle.Should().Be(sampleDataFixture.Project.InternalRomGameName);
        
        var input = importRomDialogController.Builder.Input;

        input.Filename.Should().Be(RomFilename);
        input.RomBytes.Should().HaveCountGreaterThan(100);
        input.RomSettingsOffset!.Value.Should().Be(RomUtil.LoromSettingOffset);
        
        var snesRomAnalysisResults = input.AnalysisResults!;
        snesRomAnalysisResults.RomMapMode.Should().Be(RomMapMode.LoRom);
        snesRomAnalysisResults.DetectedRomMapModeCorrectly.Should().Be(true);
        snesRomAnalysisResults.RomSpeed.Should().Be(sampleDataFixture.Project.Data.RomSpeed);
    }
}