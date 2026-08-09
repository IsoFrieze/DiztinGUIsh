using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Diz.Core.export;
using Diz.Core.util;
using Diz.Ui.ViewModels.ExportSettings;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.ExportSettings;

/// <summary>
/// ExportSettingsViewModel: the "Export Disassembly" settings screen.
///
/// Nothing here parses a line template or renders real assembly. Both of those live in the
/// assembly-writing project, which the ViewModel assembly may not reference, so they arrive as
/// delegates -- and these tests hand in whatever answer they want to see handled, including
/// answers no real parser would give.
///
/// The filesystem is faked and COUNTS ITS CALLS, because "the output directory really exists"
/// is a validator rule that hits the disk and must not be re-asked on every keystroke.
/// </summary>
public class ExportSettingsViewModelTests
{
    private sealed class FakeFilesystem : IFilesystemService
    {
        public bool Exists { get; set; } = true;
        public int DirectoryExistsCallCount { get; private set; }
        public List<string> DirectoriesCreated { get; } = [];

        public bool DirectoryExists(string outputDirectoryName)
        {
            DirectoryExistsCallCount++;
            return Exists;
        }

        public void CreateDirectory(string name)
        {
            DirectoriesCreated.Add(name);
            Exists = true;
        }
    }

    private sealed class SampleTextGenerator
    {
        public int CallCount { get; private set; }
        public LogWriterSettings LastSettingsSeen { get; private set; }

        public string Generate(LogWriterSettings settings)
        {
            CallCount++;
            LastSettingsSeen = settings;
            return $"sample for [{settings.Format}] x{settings.DataPerLine}";
        }
    }

    /// <summary>Settings whose every visible field differs from the defaults, so a round-trip can't pass by accident.</summary>
    private static LogWriterSettings NonDefaultSettings() =>
        new()
        {
            Format = "%label% %code%",
            DataPerLine = 4,
            Unlabeled = LogWriterSettings.FormatUnlabeled.ShowNone,
            Structure = LogWriterSettings.FormatStructure.SingleFile,
            NewLine = true,
            OutputExtraWhitespace = false,
            GenerateFullLine = false,
            IncludeUnusedLabels = true,
            PrintLabelSpecificComments = true,
            GeneratePlusMinusLabels = false,
            GenerateAssetLabels = false,
            FileOrFolderOutPath = "somewhere",
            ExcludedLabelAuthorsList = "bob,alice",
        };

    private static ExportSettingsViewModel MakeVm(
        LogWriterSettings settings = null,
        IFilesystemService fs = null,
        Func<string, bool> isLineTemplateValid = null,
        Func<LogWriterSettings, string> generateSampleText = null) =>
        new(
            settings ?? new LogWriterSettings(),
            fs ?? new FakeFilesystem(),
            isLineTemplateValid ?? (_ => true),
            generateSampleText ?? (_ => "sample"));

    private static List<string> RecordChanges(INotifyPropertyChanged vm)
    {
        var seen = new List<string>();
        vm.PropertyChanged += (_, args) => seen.Add(args.PropertyName);
        return seen;
    }

    // ---------------------------------------------------------------------------------------
    // The exclude-authors box holds raw text (the bug that made a second author untypeable)
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ExcludedAuthorsText_KeepsTheTrailingCommaThatWasJustTyped()
    {
        var vm = MakeVm();

        vm.ExcludedAuthorsText = "bob,";

        // the old screen normalized on every keystroke and wrote the result back, which deleted
        // this comma and made it impossible to start typing a second author.
        vm.ExcludedAuthorsText.Should().Be("bob,");
    }

    [Fact]
    public void ExcludedAuthorsText_KeepsSpacingCaseAndOrderExactlyAsTyped()
    {
        var vm = MakeVm();

        vm.ExcludedAuthorsText = "  zed ,  Bob,,bob  ";

        vm.ExcludedAuthorsText.Should().Be("  zed ,  Bob,,bob  ");
    }

    [Fact]
    public void BuildSettings_IsWhereTheAuthorListGetsNormalized()
    {
        var vm = MakeVm();

        vm.ExcludedAuthorsText = "  zed ,  Bob,,bob  ";

        // trimmed, blanks dropped, de-duplicated case-insensitively, sorted.
        vm.BuildSettings().ExcludedLabelAuthorsList.Should().Be("Bob,zed");
        vm.BuildSettings().ExcludedLabelAuthors.Should().BeEquivalentTo("Bob", "zed");
    }

    [Fact]
    public void ExcludedAuthorsText_SurvivesARoundTripThroughBuildSettings()
    {
        var vm = MakeVm();
        vm.ExcludedAuthorsText = "alice, bob";

        var rebuilt = MakeVm(vm.BuildSettings());

        rebuilt.ExcludedAuthorsText.Should().Be("alice, bob");
        rebuilt.BuildSettings().ExcludedLabelAuthorsList.Should().Be("alice,bob");
    }

    // ---------------------------------------------------------------------------------------
    // Line template: lower-cased on the way in, exactly as the old screen did
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void LineTemplate_IsLowerCasedAsItIsSet()
    {
        var vm = MakeVm();

        vm.LineTemplate = "%LABEL% %CODE% ; Some Literal Text";

        vm.LineTemplate.Should().Be("%label% %code% ; some literal text");
        vm.BuildSettings().Format.Should().Be("%label% %code% ; some literal text");
    }

    [Fact]
    public void LineTemplate_IsLowerCasedAtConstructionToo()
    {
        var vm = MakeVm(new LogWriterSettings { Format = "%LABEL%" });

        vm.LineTemplate.Should().Be("%label%");
    }

    [Fact]
    public void LineTemplateIsValid_AndTheSample_ComeFromTheInjectedDelegates()
    {
        var generator = new SampleTextGenerator();
        var vm = MakeVm(isLineTemplateValid: t => t.Contains("%label%"), generateSampleText: generator.Generate);

        vm.LineTemplate = "%label%";
        vm.LineTemplateIsValid.Should().BeTrue();
        vm.SampleOutputText.Should().Be("sample for [%label%] x8");

        vm.LineTemplate = "nonsense";
        vm.LineTemplateIsValid.Should().BeFalse();
        vm.SampleOutputText.Should().Be(ExportSettingsViewModel.InvalidLineTemplateMessage);
    }

    [Fact]
    public void CanStartExport_IsFalseWhileTheLineTemplateDoesNotParse()
    {
        var vm = MakeVm(isLineTemplateValid: t => t == "ok");

        vm.LineTemplate = "ok";
        vm.CanStartExport.Should().BeTrue();

        vm.LineTemplate = "not ok";
        vm.CanStartExport.Should().BeFalse();
        vm.StatusText.Should().Be(ExportSettingsViewModel.InvalidLineTemplateMessage);
    }

    // ---------------------------------------------------------------------------------------
    // Bytes-per-line clamping (a hand-edited project can put anything in the stored setting)
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(-99, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(8, 8)]
    [InlineData(16, 16)]
    [InlineData(17, 16)]
    [InlineData(9999, 16)]
    public void DataPerLine_IsClampedToWhatTheScreenCanShow(int assigned, int expected)
    {
        var vm = MakeVm();

        vm.DataPerLine = assigned;

        vm.DataPerLine.Should().Be(expected);
        vm.BuildSettings().DataPerLine.Should().Be(expected);
    }

    [Fact]
    public void DataPerLine_IsClampedAtConstructionSoAnOutOfRangeProjectStillOpens()
    {
        MakeVm(new LogWriterSettings { DataPerLine = 500 }).DataPerLine.Should().Be(16);
        MakeVm(new LogWriterSettings { DataPerLine = -1 }).DataPerLine.Should().Be(1);
    }

    // ---------------------------------------------------------------------------------------
    // Live validation, and the one rule that is allowed to touch the disk
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Problems_IsEmptyAndExportIsAllowedWhenEverythingIsFine()
    {
        var vm = MakeVm();

        vm.Problems.Should().BeEmpty();
        vm.StatusText.Should().BeEmpty();
        vm.CanStartExport.Should().BeTrue();
    }

    [Fact]
    public void Problems_ReportsAnEmptyOutputPathLiveAndBlocksExport()
    {
        var vm = MakeVm();

        vm.OutputPath = "";

        vm.Problems.Should().Contain(p => p.Contains("empty"));
        vm.StatusText.Should().NotBeEmpty();
        vm.CanStartExport.Should().BeFalse();
    }

    [Fact]
    public void Problems_ReportsAMissingOutputDirectoryOnceTheDiskHasBeenAsked()
    {
        var fs = new FakeFilesystem { Exists = false };
        var vm = MakeVm(fs: fs);

        vm.Problems.Should().Contain(p => p.Contains("doesn't exist"));
        vm.CanStartExport.Should().BeFalse();
        vm.NeedsOutputDirectoryCreated.Should().BeTrue();
    }

    [Fact]
    public void TypingDoesNotHitTheDisk_OnlyAnExplicitRefreshDoes()
    {
        var fs = new FakeFilesystem();
        var vm = MakeVm(fs: fs);

        var afterConstruction = fs.DirectoryExistsCallCount;

        // one keystroke at a time, the way a text box delivers them
        foreach (var text in new[] { "g", "ge", "gen", "gene", "gener", "genera", "generat", "generated" })
            vm.OutputPath = text;

        vm.DataPerLine = 4;
        vm.NewLine = true;

        fs.DirectoryExistsCallCount.Should().Be(afterConstruction,
            "the disk-hitting validator rule must run on commit or a debounce, never per keystroke");

        vm.RefreshOutputPathStatus();

        fs.DirectoryExistsCallCount.Should().BeGreaterThan(afterConstruction);
    }

    [Fact]
    public void AnUncheckedPathIsNotAnnouncedAsMissing()
    {
        var fs = new FakeFilesystem { Exists = false };
        var vm = MakeVm(fs: fs);

        vm.NeedsOutputDirectoryCreated.Should().BeTrue("construction asks the disk once");

        // a path nothing has been read about yet: no error until it is checked
        vm.OutputPath = "some/other/place";

        vm.NeedsOutputDirectoryCreated.Should().BeFalse();
        vm.Problems.Should().NotContain(p => p.Contains("doesn't exist"));

        vm.RefreshOutputPathStatus();

        vm.NeedsOutputDirectoryCreated.Should().BeTrue();
        vm.Problems.Should().Contain(p => p.Contains("doesn't exist"));
    }

    [Fact]
    public void CreateOutputDirectory_CreatesItAndClearsTheProblem()
    {
        var fs = new FakeFilesystem { Exists = false };
        var vm = MakeVm(fs: fs);

        vm.NeedsOutputDirectoryCreated.Should().BeTrue();
        var wanted = vm.OutputDirectoryToCreate;

        vm.CreateOutputDirectory();

        fs.DirectoriesCreated.Should().Contain(wanted);
        vm.NeedsOutputDirectoryCreated.Should().BeFalse();
        vm.Problems.Should().NotContain(p => p.Contains("doesn't exist"));
    }

    // ---------------------------------------------------------------------------------------
    // Single-file structure: still selectable, but says why it will fail
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void StructureWarningText_AppearsOnlyForSingleFile()
    {
        var vm = MakeVm();

        vm.Structure.Should().Be(LogWriterSettings.FormatStructure.OneBankPerFile);
        vm.StructureWarningText.Should().BeEmpty();

        vm.Structure = LogWriterSettings.FormatStructure.SingleFile;
        vm.StructureWarningText.Should().Be(ExportSettingsViewModel.SingleFileWarningText);

        vm.Structure = LogWriterSettings.FormatStructure.OneBankPerFile;
        vm.StructureWarningText.Should().BeEmpty();
    }

    [Fact]
    public void StructureWarningText_UsesTheAssemblyWritersOwnWordsAndDoesNotBlockTheChoice()
    {
        var vm = MakeVm();

        vm.Structure = LogWriterSettings.FormatStructure.SingleFile;

        vm.StructureWarningText.Should().Contain("single file output mode is broken");
        vm.BuildSettings().Structure.Should().Be(LogWriterSettings.FormatStructure.SingleFile);
    }

    [Fact]
    public void ChangingStructure_AnnouncesTheWarningToo()
    {
        var vm = MakeVm();
        var seen = RecordChanges(vm);

        vm.Structure = LogWriterSettings.FormatStructure.SingleFile;

        seen.Should().Contain(nameof(ExportSettingsViewModel.Structure));
        seen.Should().Contain(nameof(ExportSettingsViewModel.StructureWarningText));
    }

    // ---------------------------------------------------------------------------------------
    // BuildSettings: the one place the record is reassembled
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void BuildSettings_RoundTripsEveryPropertyThisScreenEdits()
    {
        var vm = MakeVm();

        vm.LineTemplate = "%label% %code%";
        vm.DataPerLine = 4;
        vm.Unlabeled = LogWriterSettings.FormatUnlabeled.ShowNone;
        vm.Structure = LogWriterSettings.FormatStructure.SingleFile;
        vm.NewLine = true;
        vm.OutputExtraWhitespace = false;
        vm.GenerateFullLine = false;
        vm.IncludeUnusedLabels = true;
        vm.PrintLabelSpecificComments = true;
        vm.GeneratePlusMinusLabels = false;
        vm.GenerateAssetLabels = false;
        vm.OutputPath = "somewhere";
        vm.ExcludedAuthorsText = "alice, bob";

        var built = vm.BuildSettings();

        built.Format.Should().Be("%label% %code%");
        built.DataPerLine.Should().Be(4);
        built.Unlabeled.Should().Be(LogWriterSettings.FormatUnlabeled.ShowNone);
        built.Structure.Should().Be(LogWriterSettings.FormatStructure.SingleFile);
        built.NewLine.Should().BeTrue();
        built.OutputExtraWhitespace.Should().BeFalse();
        built.GenerateFullLine.Should().BeFalse();
        built.IncludeUnusedLabels.Should().BeTrue();
        built.PrintLabelSpecificComments.Should().BeTrue();
        built.GeneratePlusMinusLabels.Should().BeFalse();
        built.GenerateAssetLabels.Should().BeFalse();
        built.FileOrFolderOutPath.Should().Be("somewhere");
        built.ExcludedLabelAuthorsList.Should().Be("alice,bob");
    }

    [Fact]
    public void BuildSettings_ReproducesTheSettingsItWasBuiltFrom()
    {
        var original = NonDefaultSettings();

        MakeVm(original).BuildSettings().Should().Be(original);
    }

    [Fact]
    public void BuildSettings_CarriesThroughEverythingTheScreenDoesNotShow()
    {
        var original = new LogWriterSettings
        {
            BaseOutputPath = @"C:\project",
            AssetsDirPath = "my-assets",
            ExtractedDirPath = "my-extracted",
            BuildDirPath = "my-build",
            RomSizeOverride = 1234,
            ErrorFilename = "oops.txt",
            AppendFlagTypeToComment = true,
            SuppressSingleFileModeDisabledError = true,
        };

        var vm = MakeVm(original);
        vm.DataPerLine = 3;

        var built = vm.BuildSettings();

        built.BaseOutputPath.Should().Be(@"C:\project");
        built.AssetsDirPath.Should().Be("my-assets");
        built.ExtractedDirPath.Should().Be("my-extracted");
        built.BuildDirPath.Should().Be("my-build");
        built.RomSizeOverride.Should().Be(1234);
        built.ErrorFilename.Should().Be("oops.txt");
        built.AppendFlagTypeToComment.Should().BeTrue();
        built.SuppressSingleFileModeDisabledError.Should().BeTrue();
    }

    [Fact]
    public void TheSampleSeesTheWholeSettingsRecord_NotJustTheLineTemplate()
    {
        var generator = new SampleTextGenerator();
        var vm = MakeVm(generateSampleText: generator.Generate);

        vm.DataPerLine = 2;

        generator.LastSettingsSeen.DataPerLine.Should().Be(2);
        vm.SampleOutputText.Should().Contain("x2");
    }

    // ---------------------------------------------------------------------------------------
    // No echo: writing back what is already there must be silent
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AssigningTheCurrentValueRaisesNothing()
    {
        var generator = new SampleTextGenerator();
        var vm = MakeVm(new LogWriterSettings { Format = "%label%" }, generateSampleText: generator.Generate);

        var samplesBefore = generator.CallCount;

        // read everything out first: a host binding echoes the value it was handed, so what goes
        // back in is a copy of what came out, not the property assigned to itself.
        var lineTemplate = vm.LineTemplate;
        var dataPerLine = vm.DataPerLine;
        var unlabeled = vm.Unlabeled;
        var structure = vm.Structure;
        var newLine = vm.NewLine;
        var outputExtraWhitespace = vm.OutputExtraWhitespace;
        var generateFullLine = vm.GenerateFullLine;
        var includeUnusedLabels = vm.IncludeUnusedLabels;
        var printLabelSpecificComments = vm.PrintLabelSpecificComments;
        var generatePlusMinusLabels = vm.GeneratePlusMinusLabels;
        var generateAssetLabels = vm.GenerateAssetLabels;
        var outputPath = vm.OutputPath;
        var excludedAuthorsText = vm.ExcludedAuthorsText;

        var seen = RecordChanges(vm);

        vm.LineTemplate = lineTemplate;
        vm.DataPerLine = dataPerLine;
        vm.Unlabeled = unlabeled;
        vm.Structure = structure;
        vm.NewLine = newLine;
        vm.OutputExtraWhitespace = outputExtraWhitespace;
        vm.GenerateFullLine = generateFullLine;
        vm.IncludeUnusedLabels = includeUnusedLabels;
        vm.PrintLabelSpecificComments = printLabelSpecificComments;
        vm.GeneratePlusMinusLabels = generatePlusMinusLabels;
        vm.GenerateAssetLabels = generateAssetLabels;
        vm.OutputPath = outputPath;
        vm.ExcludedAuthorsText = excludedAuthorsText;

        seen.Should().BeEmpty("a host echoing the value it was just given must not start another round");
        generator.CallCount.Should().Be(samplesBefore);
    }

    [Fact]
    public void AssigningAValueThatOnlyDiffersInCaseOfTheLineTemplateRaisesNothing()
    {
        var vm = MakeVm(new LogWriterSettings { Format = "%label%" });
        var seen = RecordChanges(vm);

        // the host round-trips through a text box that has not been lower-cased yet
        vm.LineTemplate = "%LABEL%";

        seen.Should().BeEmpty();
        vm.LineTemplate.Should().Be("%label%");
    }

    [Fact]
    public void ChangingAValueDoesAnnounceIt()
    {
        var vm = MakeVm();
        var seen = RecordChanges(vm);

        vm.DataPerLine = 3;

        seen.Should().Contain(nameof(ExportSettingsViewModel.DataPerLine));
    }

    [Fact]
    public void ClampedAssignmentsThatLandOnTheCurrentValueRaiseNothing()
    {
        var vm = MakeVm();
        vm.DataPerLine = 16;
        var seen = RecordChanges(vm);

        vm.DataPerLine = 999;

        seen.Should().BeEmpty();
        vm.DataPerLine.Should().Be(16);
    }

    // ---------------------------------------------------------------------------------------
    // Thread rule
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void EveryNotificationGoesThroughTheInjectedMarshaller()
    {
        var marshalled = 0;
        var vm = new ExportSettingsViewModel(
            new LogWriterSettings(),
            new FakeFilesystem(),
            _ => true,
            _ => "sample",
            action =>
            {
                marshalled++;
                action();
            });

        var seen = RecordChanges(vm);
        var before = marshalled;

        vm.NewLine = true;

        seen.Should().NotBeEmpty();
        marshalled.Should().BeGreaterThan(before);
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        var fs = new FakeFilesystem();

        FluentActions.Invoking(() => new ExportSettingsViewModel(null, fs, _ => true, _ => ""))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ExportSettingsViewModel(new LogWriterSettings(), null, _ => true, _ => ""))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ExportSettingsViewModel(new LogWriterSettings(), fs, null, _ => ""))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ExportSettingsViewModel(new LogWriterSettings(), fs, _ => true, null))
            .Should().Throw<ArgumentNullException>();
    }
}
