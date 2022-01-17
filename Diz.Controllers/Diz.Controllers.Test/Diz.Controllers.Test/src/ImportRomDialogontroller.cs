using System;
using System.Collections.Generic;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.model.project;
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
    
    protected override void Configure(IServiceRegistry serviceRegistry)
    {
        base.Configure(serviceRegistry);

        serviceRegistry.Register<ISampleRomTestData, SampleRomTestDataFixture>();

        serviceRegistry.Register<IReadFromFileBytes>(factory =>
        {
            var mockLinkedRomBytesProvider = TestUtil.CreateReadFromFileMock(sampleDataFixture.SampleRomBytes);
            return mockLinkedRomBytesProvider.Object;
        });

        serviceRegistry.Register(factory => new Mock<ICommonGui>().Object);
        
        serviceRegistry.Register(factory =>
        {
            var mock = new Mock<IImportRomDialogView>();
            mock.Setup(x => x.ShowAndWaitForUserToConfirmSettings())
                .Callback(
                    () => OnViewShown?.Invoke(null, EventArgs.Empty)
                ).Returns(true);
            
            mock.Setup(x => x.GetEnabledVectorTableEntries())
                .Returns(new List<string> { SnesVectorNames.Native_ABORT, SnesVectorNames.Emulation_RESET });

            return mock.Object;
        });
    }

    public event EventHandler? OnViewShown;

    private const string RomFilename = "SAMPLEROM";

    [Fact(Skip="Not working yet, WIP")]
    public void TestWorkflowWithDefaultSettings()
    {
        var mockedFileBytes = sampleDataFixture.SampleRomBytes;
        var mockLinkedRomBytesProvider = TestUtil.CreateReadFromFileMock(mockedFileBytes);

        // var mockController = new Mock<IImportRomDialogController>();
        // var controller = mockController.Object;
        // var generatedSettings = controller.PromptUserForImportOptions("SAMPLE");

        OnViewShown += (sender, args) =>
        {
            // simulate any UI here. button presses/etc.
            
            importRomDialogController.Submit();
        };
        var generatedSettings = importRomDialogController.PromptUserForImportOptions(RomFilename);

        generatedSettings.Should().NotBeNull();
        generatedSettings.RomBytes.Should().BeEquivalentTo(mockedFileBytes);
        generatedSettings.RomFilename.Should().Be(RomFilename);
    }
}
