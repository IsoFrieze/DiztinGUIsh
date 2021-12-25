#nullable enable

using Diz.Controllers.interfaces;
using Diz.Controllers.services;
using Diz.Core.export;
using Diz.Core.util;
using FluentAssertions;
using LightInject;
using Moq;
using Xunit;

namespace Diz.Test.Tests.Controllers;

public class LogCreatorSettingsEditorControllerTests
{
    public LogCreatorSettingsEditorControllerTests()
    {
        RegisterTestServices();
    }
    
    private static void RegisterTestServices()
    {
        Service.Recreate();
        Service.Container.RegisterFrom<DizControllersCompositionRoot>();
        
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

    [Fact]
    public void Basics()
    {
        var controller = Service.Container.GetInstance<ILogCreatorSettingsEditorController>();

        controller.Settings.Validate().Should().BeNull("default settings should be valid");
        controller.PromptSetupAndValidateExportSettings().Should().BeTrue("dialog unchanged settings should be valid");
    }
}