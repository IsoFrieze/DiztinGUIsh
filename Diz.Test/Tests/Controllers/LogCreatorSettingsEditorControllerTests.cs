#nullable enable

using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diz.Test.Tests.Controllers;

public class LogCreatorSettingsEditorControllerTests
{
    private static IFilesystemService CreateFilesystemMockObject()
    {
        var fsMock = new Mock<IFilesystemService>();

        fsMock.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        fsMock.Setup(x => x.CreateDirectory(It.IsAny<string>()));

        return fsMock.Object;
    }

    private static ILogCreatorSettingsEditorView CreateLogCreatorSettingsEditorView()
    {
        var viewMock = new Mock<ILogCreatorSettingsEditorView>();
        viewMock.Setup(x => x.PromptEditAndConfirmSettings()).Returns(true);
        return viewMock.Object;
    }

    [Fact]
    public void Basics()
    {
        var fsMock = CreateFilesystemMockObject();
        var viewMock = CreateLogCreatorSettingsEditorView();
        var controller = new LogCreatorSettingsEditorController(viewMock, fsMock);

        controller.ValidateExportSettings().Should().BeTrue("default settings should be valid");
        controller.PromptSetupAndValidateExportSettings().Should().BeTrue("dialog unchanged settings should be valid");
    }
}