#nullable enable

using System;
using System.IO;
using System.Linq;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.Test.Utils;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diz.Controllers.Test;

// Step 4 of the new-ui plan reshaped ProjectController.ImportLabelsCsv from
// (ILabelEditorView, bool) to (string path, bool): the controller no longer prompts or
// calls back into a view. These tests cover the reshaped seam with no dialog anywhere:
//  - success mutates the label provider (append and replace-all semantics)
//  - a parse failure applies NOTHING (all-or-nothing, as before) and surfaces byte-for-byte
//    the same dialog text the old WinformsGuiUtil.ShowLineItemError built, now via
//    ICommonGui.ShowError
//  - the historical quirk that errLine stays 0 for mid-parse exceptions (so no
//    "(Check line N.)" suffix ever appeared) is preserved bug-for-bug
public class ProjectControllerImportLabelsCsvTests : ContainerFixture
{
    [Inject] private readonly ISnesSampleProjectFactory sampleProjectFactory = null!;

    private readonly Mock<ICommonGui> commonGuiMock = new();

    private ProjectController CreateControllerWithSampleProject()
    {
        var controller = new ProjectController(
            commonGuiMock.Object,
            new Mock<IFilesystemService>().Object,
            new Mock<IControllerFactory>().Object,
            () => null!,
            _ => null!,
            () => null!);

        var project = (Project)sampleProjectFactory.Create();

        // Project has a private setter (normally assigned only by the open/import flows,
        // which drag in real views); set it directly for this focused unit test.
        typeof(ProjectController)
            .GetProperty(nameof(ProjectController.Project))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(controller, [project]);

        return controller;
    }

    private static void RunWithTempCsv(string contents, Action<string> testBody)
    {
        var path = Path.Combine(Path.GetTempPath(), $"diz-test-labels-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, contents);
        try
        {
            testBody(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Append_ValidCsv_AddsLabels_AndShowsNoError()
    {
        var controller = CreateControllerWithSampleProject();
        var labels = controller.Project.Data.Labels;
        var countBefore = labels.Labels.Count();

        RunWithTempCsv("7E9999,label_from_csv,imported comment",
            path => controller.ImportLabelsCsv(path, replaceAll: false));

        var imported = labels.GetLabel(0x7E9999);
        imported.Should().NotBeNull();
        imported!.Name.Should().Be("label_from_csv");
        imported.Comment.Should().Be("imported comment");
        labels.Labels.Count().Should().Be(countBefore + 1, "append must keep existing labels");

        commonGuiMock.Verify(x => x.ShowError(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ReplaceAll_ValidCsv_DeletesEverythingFirst()
    {
        var controller = CreateControllerWithSampleProject();
        var labels = controller.Project.Data.Labels;
        labels.AddLabel(0x7E0001, new Label { Name = "doomed_by_replace" }, overwrite: true);

        RunWithTempCsv("7E9999,sole_survivor,x",
            path => controller.ImportLabelsCsv(path, replaceAll: true));

        labels.GetLabel(0x7E0001).Should().BeNull("replaceAll deletes all pre-existing labels");
        labels.Labels.Count().Should().Be(1);
        labels.GetLabel(0x7E9999)!.Name.Should().Be("sole_survivor");

        commonGuiMock.Verify(x => x.ShowError(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void UnknownExtension_ShowsExactLegacyErrorText()
    {
        var controller = CreateControllerWithSampleProject();

        controller.ImportLabelsCsv("labels.unknownextension", replaceAll: false);

        // byte-for-byte the dialog text the old view-callback path produced
        // (WinformsGuiUtil.ShowLineItemError with errLine == 0: no line-number suffix)
        commonGuiMock.Verify(x => x.ShowError(
            "An error occurred while parsing the file.\n" +
            "No importer was found that can import a file named:\n'labels.unknownextension'"),
            Times.Once);
    }

    [Fact]
    public void MalformedCsv_AppliesNothing_AndShowsErrorWithoutLineNumber()
    {
        var controller = CreateControllerWithSampleProject();
        var labels = controller.Project.Data.Labels;
        var countBefore = labels.Labels.Count();

        // line 2 has a non-hex address, so parsing throws mid-file. all-or-nothing:
        // even the valid line 1 must NOT be applied.
        RunWithTempCsv("7E9999,valid_line,ok\nzzzz,bad_address,boom",
            path => controller.ImportLabelsCsv(path, replaceAll: false));

        labels.Labels.Count().Should().Be(countBefore, "a failed import applies nothing");
        labels.GetLabel(0x7E9999).Should().BeNull();

        // bug-for-bug: ImportLabelsFromCsv assigns errLine only on a code path that a
        // mid-parse throw never reaches, so the old dialog never showed "(Check line N.)".
        commonGuiMock.Verify(x => x.ShowError(It.Is<string>(msg =>
            msg.StartsWith("An error occurred while parsing the file.\n") &&
            !msg.Contains("Check line"))),
            Times.Once);
    }

    [Fact]
    public void EmptyPath_DoesNothing()
    {
        var controller = CreateControllerWithSampleProject();

        controller.ImportLabelsCsv("", replaceAll: false);

        commonGuiMock.Verify(x => x.ShowError(It.IsAny<string>()), Times.Never);
    }
}
