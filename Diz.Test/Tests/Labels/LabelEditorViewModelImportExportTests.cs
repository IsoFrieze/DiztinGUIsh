using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Ui.ViewModels.Labels;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.Labels;

/// <summary>
/// ImportLabelsAsync / ExportLabelsAsync -- wrapping the existing LabelImporter /
/// LabelExporterCsv, including the full CT US corpus round-trip through the VM layer.
/// </summary>
public class LabelEditorViewModelImportExportTests
{
    private static LabelsServiceWithTemp NewProvider(
        params (int addr, string name, string comment)[] entries)
    {
        var svc = new LabelsServiceWithTemp(null!);
        foreach (var (addr, name, comment) in entries)
            svc.AddLabel(addr, new Label { Name = name, Comment = comment });
        return svc;
    }

    private static string TempCsvPath() =>
        Path.Combine(Path.GetTempPath(), $"diz_vm_labels_{Guid.NewGuid():N}.csv");

    /// <summary>synchronous IProgress: Progress&lt;T&gt; posts via SynchronizationContext,
    /// which is nondeterministic under xunit; this records inline.</summary>
    private sealed class RecordingProgress : IProgress<int>
    {
        public List<int> Reports { get; } = [];
        public void Report(int value) => Reports.Add(value);
    }

    [Fact]
    public async Task SmallRoundTrip_ExportThenImport_ThroughTheVm()
    {
        var path = TempCsvPath();
        try
        {
            using var sourceVm = new LabelEditorViewModel(NewProvider(
                (0x808000, "reset_vector", "entry, with a comma"),
                (0x7E0100, "player_hp", ""),
                (0x7E0102, "", "empty names are load-bearing")));

            var exportResult = await sourceVm.ExportLabelsAsync(path);
            exportResult.LabelsWritten.Should().Be(3);
            exportResult.Sanitizations.Should().BeEmpty();

            var destProvider = NewProvider((0x010000, "should_vanish", ""));
            using var destVm = new LabelEditorViewModel(destProvider);

            var importResult = await destVm.ImportLabelsAsync(path, replaceAll: true);

            importResult.Success.Should().BeTrue();
            importResult.LabelsReadFromFile.Should().Be(3);
            destVm.TotalLabelCount.Should().Be(3);
            destProvider.GetLabel(0x010000).Should().BeNull("replaceAll deletes everything first");
            destProvider.GetLabel(0x808000)!.Comment.Should().Be("entry, with a comma");
            destProvider.GetLabel(0x7E0102)!.Name.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Import_ReplaceAllFalse_MergesIntoExistingLabels()
    {
        var path = TempCsvPath();
        try
        {
            using var sourceVm = new LabelEditorViewModel(NewProvider((0x020000, "incoming", "new")));
            await sourceVm.ExportLabelsAsync(path);

            var destProvider = NewProvider((0x010000, "existing", "keep"));
            using var destVm = new LabelEditorViewModel(destProvider);

            var result = await destVm.ImportLabelsAsync(path, replaceAll: false);

            result.Success.Should().BeTrue();
            destVm.TotalLabelCount.Should().Be(2);
            destProvider.GetLabel(0x010000)!.Name.Should().Be("existing", "unmatched labels are left alone");
            destProvider.GetLabel(0x020000)!.Name.Should().Be("incoming");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Import_MissingFile_FailsWithResultAndErrorRaised_AndIsBusyResets()
    {
        using var vm = new LabelEditorViewModel(NewProvider((0x010000, "untouched", "")));
        var errors = new List<string>();
        vm.ErrorRaised += (_, msg) => errors.Add(msg);

        var result = await vm.ImportLabelsAsync(
            Path.Combine(Path.GetTempPath(), $"does_not_exist_{Guid.NewGuid():N}.csv"),
            replaceAll: true);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        errors.Should().ContainSingle();
        vm.IsBusy.Should().BeFalse();
        vm.TotalLabelCount.Should().Be(1, "a failed parse must not delete anything, even with replaceAll");
    }

    [Fact]
    public async Task Import_UnknownFileKind_Fails()
    {
        using var vm = new LabelEditorViewModel(NewProvider());
        var result = await vm.ImportLabelsAsync("labels.unknownextension", replaceAll: false);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No importer");
    }

    [Fact]
    public async Task Import_MalformedCsv_ReportsErrorLineNumber()
    {
        var path = TempCsvPath();
        try
        {
            await File.WriteAllLinesAsync(path, [
                "808000,good_label,fine",
                "not-hex-at-all,bad,line 2 must be reported",
            ]);
            using var vm = new LabelEditorViewModel(NewProvider());

            var result = await vm.ImportLabelsAsync(path, replaceAll: true);

            result.Success.Should().BeFalse();
            result.ErrorLineNumber.Should().Be(2);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Import_ReportsCoarseProgress()
    {
        var path = TempCsvPath();
        try
        {
            using var sourceVm = new LabelEditorViewModel(NewProvider((0x010000, "x", "")));
            await sourceVm.ExportLabelsAsync(path);

            using var vm = new LabelEditorViewModel(NewProvider());
            var progress = new RecordingProgress();

            await vm.ImportLabelsAsync(path, replaceAll: true, progress);

            progress.Reports.Should().StartWith(0);
            progress.Reports.Should().EndWith(100);
            progress.Reports.Should().Contain(50);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Import_PreCancelledToken_Throws_AndMutatesNothing()
    {
        var provider = NewProvider((0x010000, "survivor", ""));
        using var vm = new LabelEditorViewModel(provider);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            vm.ImportLabelsAsync("whatever.csv", replaceAll: true, ct: cts.Token));

        provider.GetLabel(0x010000).Should().NotBeNull();
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Export_PreCancelledToken_Throws()
    {
        using var vm = new LabelEditorViewModel(NewProvider((0x010000, "x", "")));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            vm.ExportLabelsAsync(TempCsvPath(), cts.Token));
        vm.IsBusy.Should().BeFalse();
    }

    // =====================================================================================
    // the real thing: full CT US corpus through the VM layer
    // =====================================================================================

    [Fact]
    public async Task CtCorpus_ExportThenReimport_ThroughTheVm_RoundTrips()
    {
        if (!CtLabelCorpus.IsAvailable)
            return; // corpus lives in a sibling worktree; skip rather than fail when absent

        var corpus = CtLabelCorpus.Load();
        corpus.Count.Should().BeGreaterThan(8000, "the corpus test must not pass vacuously");

        var sourceProvider = NewProvider();
        sourceProvider.SetAll(new Dictionary<int, IAnnotationLabel>(corpus));
        using var sourceVm = new LabelEditorViewModel(sourceProvider);
        sourceVm.TotalLabelCount.Should().Be(corpus.Count);

        var path = TempCsvPath();
        try
        {
            var exportResult = await sourceVm.ExportLabelsAsync(path);
            exportResult.LabelsWritten.Should().Be(corpus.Count);

            var destProvider = NewProvider();
            using var destVm = new LabelEditorViewModel(destProvider);
            var importResult = await destVm.ImportLabelsAsync(path, replaceAll: true);

            importResult.Success.Should().BeTrue();
            importResult.LabelsReadFromFile.Should().Be(corpus.Count);
            destVm.TotalLabelCount.Should().Be(corpus.Count);

            // names must survive exactly (Step 1 made the corpus 100% name-clean).
            // comments survive except the fields the exporter reported sanitizing
            // (newlines collapse to spaces -- CSV is lossy interchange, XML is canonical).
            var sanitizedComments = exportResult.Sanitizations
                .Where(s => s.Field == "Comment")
                .Select(s => s.SnesAddress)
                .ToHashSet();

            var nameMismatches = 0;
            var commentMismatches = 0;
            foreach (var (addr, original) in corpus)
            {
                var reimported = destProvider.GetLabel(addr);
                reimported.Should().NotBeNull($"address {addr:X6} must survive the round-trip");
                if (reimported!.Name != original.Name)
                    nameMismatches++;
                if (!sanitizedComments.Contains(addr) && reimported.Comment != original.Comment)
                    commentMismatches++;
            }

            nameMismatches.Should().Be(0);
            commentMismatches.Should().Be(0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CtCorpus_LoadsIntoVm_AndSearchFiltersIt()
    {
        if (!CtLabelCorpus.IsAvailable)
            return;

        var corpus = CtLabelCorpus.Load();
        corpus.Count.Should().BeGreaterThan(8000, "the corpus test must not pass vacuously");

        var provider = NewProvider();
        provider.SetAll(new Dictionary<int, IAnnotationLabel>(corpus));
        using var vm = new LabelEditorViewModel(provider);

        vm.TotalLabelCount.Should().Be(corpus.Count);
        vm.Rows.Select(r => r.SnesAddress).Should().BeInAscendingOrder();

        vm.SearchTerm = "is:ram";
        vm.VisibleLabelCount.Should().BeGreaterThan(0).And.BeLessThan(corpus.Count);

        vm.ClearSearch();
        vm.VisibleLabelCount.Should().Be(corpus.Count);
    }
}
