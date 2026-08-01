using System;
using System.Collections.Generic;
using Diz.Ui.ViewModels.MisalignmentChecker;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.MisalignmentChecker;

/// <summary>
/// MisalignmentCheckerViewModel: a report, a count, and one sentence describing them, produced
/// by a scan the ViewModel does not implement.
///
/// The scan is a caller-supplied delegate because the real one
/// (SnesApiExtensions.GenerateMisalignmentReport) lives in Diz.Cpu.65816, which the ViewModel
/// assembly may not reference. Every test here therefore supplies its own fake sweep, which also
/// makes "when is the delegate allowed to run" directly observable -- and it is not allowed to
/// run at construction, because the real one walks the whole ROM.
///
/// The 500 wording is pinned deliberately: the real generator checks its limit once per STEP and
/// a step can contribute several findings, so a capped scan reports at least 500, sometimes more.
/// Wording that claimed "the first 500" would be wrong for exactly that case.
/// </summary>
public class MisalignmentCheckerViewModelTests
{
    /// <summary>A stand-in sweep that counts how many times it was asked to run.</summary>
    private sealed class FakeScan(int found, string reportText)
    {
        public int TimesRun { get; private set; }

        public (int found, string reportText) Run()
        {
            TimesRun++;
            return (found, reportText);
        }
    }

    private static List<string> RecordNotifications(MisalignmentCheckerViewModel vm)
    {
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }

    // ------------------------------------------------------------------
    // construction
    // ------------------------------------------------------------------

    [Fact]
    public void AFreshlyBuiltViewModelHasScannedNothingAndSaysNothing()
    {
        var scan = new FakeScan(3, "three of them");

        var vm = new MisalignmentCheckerViewModel(scan.Run);

        scan.TimesRun.Should().Be(0, "the sweep walks the whole ROM; it waits to be asked");
        vm.ReportText.Should().BeEmpty();
        vm.FoundCount.Should().BeNull("null is 'not asked', which is not the same as 'found none'");
        vm.StatusText.Should().BeEmpty();
    }

    [Fact]
    public void AScanIsRequired()
    {
        var make = () => new MisalignmentCheckerViewModel(null!);
        make.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------
    // scanning
    // ------------------------------------------------------------------

    [Fact]
    public void ScanningTakesOnBothHalvesOfTheResult()
    {
        var scan = new FakeScan(2, "C00010 (0x10): Operand without Opcode\r\n");
        var vm = new MisalignmentCheckerViewModel(scan.Run);

        vm.Scan();

        scan.TimesRun.Should().Be(1);
        vm.ReportText.Should().Be("C00010 (0x10): Operand without Opcode\r\n");
        vm.FoundCount.Should().Be(2);
        vm.StatusText.Should().Be("Found 2 misalignments");
    }

    [Fact]
    public void ScanningNotifiesForEveryPropertyItChanged()
    {
        var vm = new MisalignmentCheckerViewModel(new FakeScan(1, "one").Run);
        var raised = RecordNotifications(vm);

        vm.Scan();

        raised.Should().Contain(nameof(MisalignmentCheckerViewModel.ReportText));
        raised.Should().Contain(nameof(MisalignmentCheckerViewModel.FoundCount));
        raised.Should().Contain(nameof(MisalignmentCheckerViewModel.StatusText),
            "it is derived, so nothing else would tell a bound view to re-read it");
    }

    [Fact]
    public void ASecondScanReplacesEverythingTheFirstOneProduced()
    {
        // the window's whole workflow: scan, go fix something by hand, scan again. The second
        // answer must not be blended with the first.
        var results = new Queue<(int, string)>([(4, "four of them"), (0, "No misaligned flags found!")]);
        var vm = new MisalignmentCheckerViewModel(results.Dequeue);

        vm.Scan();
        vm.Scan();

        vm.ReportText.Should().Be("No misaligned flags found!");
        vm.FoundCount.Should().Be(0);
        vm.StatusText.Should().Be("No misalignments found");
    }

    [Fact]
    public void TheScanRunsExactlyOncePerRequest()
    {
        var scan = new FakeScan(1, "one");
        var vm = new MisalignmentCheckerViewModel(scan.Run);

        vm.Scan();
        vm.Scan();
        vm.Scan();

        // reading the results must not re-run anything either
        _ = vm.ReportText;
        _ = vm.FoundCount;
        _ = vm.StatusText;

        scan.TimesRun.Should().Be(3);
    }

    [Fact]
    public void AReportOfNothingAtAllIsHeldAsEmptyTextRatherThanNull()
    {
        var vm = new MisalignmentCheckerViewModel(() => (0, null!));

        vm.Scan();

        vm.ReportText.Should().NotBeNull().And.BeEmpty();
    }

    // ------------------------------------------------------------------
    // wording
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0, "No misalignments found")]
    [InlineData(1, "Found 1 misalignment")]
    [InlineData(2, "Found 2 misalignments")]
    [InlineData(499, "Found 499 misalignments")]
    public void TheCountIsDescribedInOneSentence(int found, string expected)
    {
        var vm = new MisalignmentCheckerViewModel(() => (found, "report"));

        vm.Scan();

        vm.StatusText.Should().Be(expected);
    }

    [Theory]
    [InlineData(500, "Found 500 misalignments (scan stopped at the 500-result limit; there may be more)")]
    [InlineData(503, "Found 503 misalignments (scan stopped at the 500-result limit; there may be more)")]
    public void ACappedScanSaysSoAndStillQuotesItsRealCount(int found, string expected)
    {
        // 503 is not hypothetical: the generator tests its limit once per step, and a single
        // multi-byte step can contribute several findings past it.
        found.Should().BeGreaterThanOrEqualTo(MisalignmentCheckerViewModel.FindingLimit);

        var vm = new MisalignmentCheckerViewModel(() => (found, "report"));

        vm.Scan();

        vm.StatusText.Should().Be(expected);
    }

    // ------------------------------------------------------------------
    // marshaller discipline
    // ------------------------------------------------------------------

    [Fact]
    public void EveryNotificationGoesThroughTheMarshaller()
    {
        // the thread rule: a host's marshaller is the only path to the UI thread, so nothing may
        // be raised around it.
        var queued = new List<Action>();
        var vm = new MisalignmentCheckerViewModel(() => (7, "seven"), queued.Add);
        var raised = RecordNotifications(vm);

        vm.Scan();

        raised.Should().BeEmpty("nothing may bypass the marshaller");
        vm.FoundCount.Should().Be(7, "the state changes immediately; only the telling is deferred");
        vm.StatusText.Should().Be("Found 7 misalignments");

        foreach (var queuedAction in queued)
            queuedAction();

        raised.Should().Contain(nameof(MisalignmentCheckerViewModel.ReportText));
        raised.Should().Contain(nameof(MisalignmentCheckerViewModel.FoundCount));
        raised.Should().Contain(nameof(MisalignmentCheckerViewModel.StatusText));
    }
}
