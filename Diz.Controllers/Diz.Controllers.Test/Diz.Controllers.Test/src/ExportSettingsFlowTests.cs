#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.LogWriter;
using Diz.LogWriter.util;
using Diz.Test.Utils;
using Diz.Ui.ViewModels.ExportSettings;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diz.Controllers.Test;

/// <summary>
/// The retry loop around the export-settings screen. It runs when what the user chose cannot be
/// exported with: they are asked whether to go back and fix it, and the screen comes up again.
///
/// The point of these tests is that the SAME screen state comes back. Rebuilding it from the
/// project on every retry throws away everything typed so far, at exactly the moment the user is
/// being asked to correct a typo in what they typed -- which is what used to happen.
/// </summary>
public class ExportSettingsFlowTests : ContainerFixture
{
    /// <summary>A path fragment the fake filesystem below reports as not existing.</summary>
    private const string MissingDirectory = "this_directory_is_not_there";

    private const string TypedLineTemplate = "%label% %code%";

    [Inject] private readonly ISnesSampleProjectFactory sampleProjectFactory = null!;

    /// <summary>
    /// Stands in for the real screen: records the ViewModel it was handed and runs one scripted
    /// edit against it. A fresh one is resolved per invocation, exactly as the real seam is.
    /// </summary>
    private sealed class ScriptedView(Action<ExportSettingsViewModel> edit, bool confirm) : IExportSettingsView
    {
        public Task<bool> EditAsync(ExportSettingsViewModel viewModel)
        {
            edit(viewModel);
            return Task.FromResult(confirm);
        }
    }

    private sealed class StubSampleGenerator : ISampleAssemblyTextGenerator
    {
        public LogCreatorOutput.OutputResult GetSampleAssemblyOutput() =>
            new() { Success = true, ErrorCount = 0, AssemblyOutputStr = "sample" };
    }

    /// <summary>
    /// Every directory exists except one whose name contains <see cref="MissingDirectory"/>, so a
    /// test can make the settings invalid by typing a path and nothing else.
    /// </summary>
    private static IFilesystemService CreateFilesystem()
    {
        var fsMock = new Mock<IFilesystemService>();
        fsMock.Setup(x => x.DirectoryExists(It.IsAny<string>()))
            .Returns((string? name) => name?.Contains(MissingDirectory) != true);
        return fsMock.Object;
    }

    private static ProjectController CreateController(
        IViewFactory viewFactory, ICommonGui commonGui, ISnesSampleProjectFactory projectFactory)
    {
        var controller = new ProjectController(
            commonGui,
            CreateFilesystem(),
            new Mock<IControllerFactory>().Object,
            viewFactory,
            _ => new StubSampleGenerator(),
            () => null!,
            _ => null!,
            () => null!);

        var project = (Project)projectFactory.Create();

        // Project has a private setter (normally assigned only by the open/import flows, which
        // drag in real views); set it directly for this focused unit test.
        typeof(ProjectController)
            .GetProperty(nameof(ProjectController.Project))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(controller, [project]);

        return controller;
    }

    /// <summary>An IViewFactory whose export-settings seam hands out the given views in order.</summary>
    private static IViewFactory ViewFactoryServing(Queue<IExportSettingsView> views)
    {
        var viewFactoryMock = new Mock<IViewFactory>();
        viewFactoryMock.Setup(x => x.GetExportSettingsView()).Returns(views.Dequeue);
        return viewFactoryMock.Object;
    }

    [Fact]
    public async Task RetryingKeepsWhatWasAlreadyTyped()
    {
        var seen = new List<ExportSettingsViewModel>();
        var templateOnSecondShowing = "";

        var views = new Queue<IExportSettingsView>();
        views.Enqueue(new ScriptedView(vm =>
        {
            seen.Add(vm);
            vm.LineTemplate = TypedLineTemplate;
            vm.OutputPath = MissingDirectory + "\\out";
        }, confirm: true));
        views.Enqueue(new ScriptedView(vm =>
        {
            seen.Add(vm);
            templateOnSecondShowing = vm.LineTemplate;
            vm.OutputPath = "generated";
        }, confirm: true));

        var commonGuiMock = new Mock<ICommonGui>();
        commonGuiMock.Setup(x => x.PromptToConfirmAction(It.IsAny<string>())).Returns(true);

        var controller = CreateController(
            ViewFactoryServing(views), commonGuiMock.Object, sampleProjectFactory);

        var settings = await controller.ShowSettingsEditorUntilValidAsync();

        seen.Should().HaveCount(2, "the first attempt was not exportable, so the screen comes back");
        seen[0].Should().BeSameAs(seen[1], "the same screen state is re-shown, not a fresh one");
        templateOnSecondShowing.Should().Be(TypedLineTemplate,
            "edits made before the failed attempt must survive the retry");

        settings.Should().NotBeNull();
        settings!.Format.Should().Be(TypedLineTemplate);
        settings.FileOrFolderOutPath.Should().Be("generated");
    }

    [Fact]
    public async Task CancellingTheRetryPromptAbandonsTheExport()
    {
        var views = new Queue<IExportSettingsView>();
        views.Enqueue(new ScriptedView(vm => vm.OutputPath = MissingDirectory + "\\out", confirm: true));

        var commonGuiMock = new Mock<ICommonGui>();
        commonGuiMock.Setup(x => x.PromptToConfirmAction(It.IsAny<string>())).Returns(false);

        var controller = CreateController(
            ViewFactoryServing(views), commonGuiMock.Object, sampleProjectFactory);

        (await controller.ShowSettingsEditorUntilValidAsync()).Should().BeNull();
        views.Should().BeEmpty("the screen is shown exactly once when the user declines to retry");
    }

    [Fact]
    public async Task ClosingTheScreenAbandonsTheExportWithoutAsking()
    {
        var views = new Queue<IExportSettingsView>();
        views.Enqueue(new ScriptedView(_ => { }, confirm: false));

        var commonGuiMock = new Mock<ICommonGui>();

        var controller = CreateController(
            ViewFactoryServing(views), commonGuiMock.Object, sampleProjectFactory);

        (await controller.ShowSettingsEditorUntilValidAsync()).Should().BeNull();
        commonGuiMock.Verify(x => x.PromptToConfirmAction(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// The DI factory the sample-output box is rendered through. It outlives the controller this
    /// conversion deleted, and is what the ViewModel's sample delegate is built from.
    /// </summary>
    [Inject] private readonly Func<LogWriterSettings, ISampleAssemblyTextGenerator> createSampleTextFn = null!;

    [Fact]
    public void TestSampleTextGeneration()
    {
        createSampleTextFn.Should().NotBeNull();
        var x = createSampleTextFn(new LogWriterSettings());
    }
}
