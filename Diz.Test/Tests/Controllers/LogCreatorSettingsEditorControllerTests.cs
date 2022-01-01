#nullable enable

using Diz.Controllers.interfaces;
using Diz.Controllers.services;
using Diz.Core.export;
using Diz.Core.util;
using Diz.Test.Utils;
using FluentAssertions;
using LightInject;
using Moq;
using Xunit;

namespace Diz.Test.Tests.Controllers;

public class LogCreatorSettingsEditorControllerTests
{
    private bool costructorRan = false;
    
    public LogCreatorSettingsEditorControllerTests()
    {
        RegisterTestServices();

        costructorRan = true;
    }
    
    private void RegisterTestServices()
    {
        AppServicesForTests.RegisterNormalAppServices();
        
        Service.Container.Register(factory =>
        {
            var fsMock = new Mock<IFilesystemService>();

            fsMock.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
            fsMock.Setup(x => x.CreateDirectory(It.IsAny<string>()));

            return fsMock.Object;
        });
        
        Service.Container.Register(factory =>
        {
            var viewMock = new Mock<ILogCreatorSettingsEditorView>();

            viewMock.Setup(x => x.PromptEditAndConfirmSettings()).Returns(true);

            return viewMock.Object;
        });
    }

    private void VerifyTestingSetup()
    {
        costructorRan.Should().BeTrue();
        
        // verify mocks are setup right
        var fs = Service.Container.GetInstance<IFilesystemService>();
        fs.GetType().Name.Should().Contain("Proxy");
    }

    [Fact]
    public void Basics()
    {
        VerifyTestingSetup();
        
        var controller = Service.Container.GetInstance<ILogCreatorSettingsEditorController>();

        controller.Settings.Validate().Should().BeNull("default settings should be valid");
        controller.PromptSetupAndValidateExportSettings().Should().BeTrue("dialog unchanged settings should be valid");
    }
    
    // TODO: remove duplicate test, was just testing stuff.
    [Fact]
    public void Basics2()
    {
        VerifyTestingSetup();
        
        var controller = Service.Container.GetInstance<ILogCreatorSettingsEditorController>();

        controller.Settings.Validate().Should().BeNull("default settings should be valid");
        controller.PromptSetupAndValidateExportSettings().Should().BeTrue("dialog unchanged settings should be valid");
    }
}