using System;
using System.Collections.Generic;
using System.Linq;
using Diz.Core.Interfaces;
using Diz.Ui.ViewModels.ImportRom;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.ImportRom;

/// <summary>
/// SnesImportRomViewModel: the options offered when a SNES ROM becomes a new project.
///
/// The vector snapshots below are synthetic -- no ROM is read anywhere in this file -- which is
/// the point of the design: the ViewModel is handed a delegate that turns a map mode into a
/// snapshot, so the tests can hand it whatever answer they want to see handled, including an
/// answer no real ROM would give.
///
/// The name list mirrors the sixteen 65816 vector-table slots in table order, four of which are
/// reserved slots the SNES never uses. Those four are ALWAYS-ON: they are the subject of the D2
/// tests below and are deliberate, not an oversight.
/// </summary>
public class SnesImportRomViewModelTests
{
    private const string NativeReserved1 = "Native_Reserved1__ignored";
    private const string NativeReserved2 = "Native_Reserved2__ignored";
    private const string NativeCop = "Native_COP";
    private const string NativeNmi = "Native_NMI";
    private const string NativeIrq = "Native_IRQ";
    private const string EmulationReserved1 = "Emulation_Reserved1__ignored";
    private const string EmulationReserved2 = "Emulation_Reserved2__ignored";
    private const string EmulationReset = "Emulation_RESET";

    private static readonly string[] AllVectorNames =
    [
        NativeReserved1, NativeReserved2, NativeCop, NativeNmi, NativeIrq,
        EmulationReserved1, EmulationReserved2, EmulationReset,
    ];

    /// <summary>The four slots with no user-facing control; always on, never selectable.</summary>
    private static readonly string[] AlwaysOnVectorNames =
    [
        NativeReserved1, NativeReserved2, EmulationReserved1, EmulationReserved2,
    ];

    private static readonly string[] SelectableVectorNames =
        AllVectorNames.Except(AlwaysOnVectorNames).ToArray();

    private static SnesVectorSnapshot ReadableSnapshot(string cartridgeTitle = "TEST CART") =>
        new(
            cartridgeTitle,
            VectorsReadable: true,
            AllVectorNames
                .Select((name, i) => new SnesVectorValue(name, $"80{i:X2}", IsReadable: true))
                .ToList());

    private static SnesVectorSnapshot UnreadableSnapshot() =>
        SnesVectorSnapshot.Unreadable(AllVectorNames);

    private sealed class Recomputer
    {
        private readonly Func<RomMapMode, SnesVectorSnapshot> produce;

        public Recomputer(Func<RomMapMode, SnesVectorSnapshot> produce) => this.produce = produce;

        public int CallCount { get; private set; }
        public List<RomMapMode> ModesAskedAbout { get; } = [];

        public SnesVectorSnapshot Recompute(RomMapMode mode)
        {
            CallCount++;
            ModesAskedAbout.Add(mode);
            return produce(mode);
        }
    }

    private static SnesImportRomViewModel MakeVm(
        SnesVectorSnapshot initial = null,
        RomMapMode detected = RomMapMode.LoRom,
        bool detectionSucceeded = true,
        string romSpeedText = "SlowRom",
        IEnumerable<string> initiallyEnabled = null,
        Func<RomMapMode, SnesVectorSnapshot> recompute = null,
        Action<Action> marshaller = null) =>
        new(
            initial ?? ReadableSnapshot(),
            detected,
            detectionSucceeded,
            romSpeedText,
            AlwaysOnVectorNames,
            initiallyEnabled ?? SelectableVectorNames,
            recompute ?? (_ => ReadableSnapshot()),
            marshaller);

    private static List<string> RecordNotifications(SnesImportRomViewModel vm)
    {
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);
        return raised;
    }

    // ------------------------------------------------------------------
    // construction / seeding
    // ------------------------------------------------------------------

    [Fact]
    public void TheRowsComeFromTheInitialSnapshotInVectorTableOrder()
    {
        var vm = MakeVm();

        vm.Vectors.Select(row => row.Name).Should().Equal(AllVectorNames);
        vm.Vectors[2].DisplayValue.Should().Be("8002");
    }

    [Fact]
    public void TheCartridgeTitleAndSpeedComeFromTheCaller()
    {
        var vm = MakeVm(ReadableSnapshot("CHRONO TRIGGER"), romSpeedText: "FastRom");

        vm.CartridgeTitle.Should().Be("CHRONO TRIGGER");
        vm.RomSpeedText.Should().Be("FastRom");
    }

    [Fact]
    public void TheSelectedModeStartsOutAsTheDetectedOne()
    {
        var vm = MakeVm(detected: RomMapMode.ExHiRom);

        vm.SelectedRomMapMode.Should().Be(RomMapMode.ExHiRom);
        vm.DetectedRomMapMode.Should().Be(RomMapMode.ExHiRom);
    }

    [Fact]
    public void EveryMappingIsOfferedAsAChoice()
    {
        var vm = MakeVm();

        vm.RomMapModeChoices.Should().Equal(Enum.GetValues<RomMapMode>());
    }

    [Fact]
    public void TheDetectionMessageNamesTheDetectedMappingWhenThereIsOne()
    {
        var vm = MakeVm(detected: RomMapMode.Sa1Rom);

        vm.DetectionMessage.Should().Be("SA - 1 ROM", "the enum carries that description");
    }

    [Fact]
    public void TheDetectionMessageSaysSoWhenNothingWasDetected()
    {
        var vm = MakeVm(detectionSucceeded: false);

        vm.DetectionMessage.Should().Be(SnesImportRomViewModel.DetectionFailedMessage);
    }

    [Fact]
    public void ADelegateAndASnapshotAreRequired()
    {
        var noSnapshot = () => new SnesImportRomViewModel(
            null!, RomMapMode.LoRom, true, "", AlwaysOnVectorNames, [], _ => ReadableSnapshot());
        var noDelegate = () => new SnesImportRomViewModel(
            ReadableSnapshot(), RomMapMode.LoRom, true, "", AlwaysOnVectorNames, [], null!);

        noSnapshot.Should().Throw<ArgumentNullException>();
        noDelegate.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------
    // D1: everything downstream follows the SELECTED mode
    // ------------------------------------------------------------------

    [Fact]
    public void ChangingTheMapModeRereadsTheVectorsAndTheCartridgeTitle()
    {
        var recomputer = new Recomputer(_ =>
            new SnesVectorSnapshot(
                "HIROM TITLE",
                VectorsReadable: true,
                AllVectorNames.Select(n => new SnesVectorValue(n, "FFC0", true)).ToList()));

        var vm = MakeVm(recompute: recomputer.Recompute);

        vm.SelectedRomMapMode = RomMapMode.HiRom;

        vm.CartridgeTitle.Should().Be("HIROM TITLE");
        vm.Vectors.Should().OnlyContain(row => row.DisplayValue == "FFC0");
        recomputer.ModesAskedAbout.Should().Equal([RomMapMode.HiRom],
            "the values must come from the mode the user picked, not the detected one");
    }

    [Fact]
    public void ChangingTheMapModeAsksTheRecomputeDelegateExactlyOnce()
    {
        // the re-entrancy hazard: applying a snapshot writes DisplayValue/IsSelectable/IsEnabled
        // on every row, and a host that echoes those writes back must not kick off a second pass.
        var recomputer = new Recomputer(_ => ReadableSnapshot());
        var vm = MakeVm(recompute: recomputer.Recompute);

        vm.SelectedRomMapMode = RomMapMode.HiRom;

        recomputer.CallCount.Should().Be(1);
    }

    [Fact]
    public void AnEchoedWriteBackIntoTheMapModeDuringARecomputeIsIgnored()
    {
        // the snapshot must differ from the current one, or no row raises anything and the echo
        // never happens.
        var recomputer = new Recomputer(_ => UnreadableSnapshot());
        var vm = MakeVm(recompute: recomputer.Recompute);

        // stand in for a two-way binding that reacts to a row change by re-asserting the mode
        vm.Vectors[2].PropertyChanged += (_, _) => vm.SelectedRomMapMode = RomMapMode.ExLoRom;

        vm.SelectedRomMapMode = RomMapMode.HiRom;

        recomputer.CallCount.Should().Be(1);
        vm.SelectedRomMapMode.Should().Be(RomMapMode.HiRom);
    }

    [Fact]
    public void SelectingTheModeThatIsAlreadySelectedRecomputesNothing()
    {
        var recomputer = new Recomputer(_ => ReadableSnapshot());
        var vm = MakeVm(detected: RomMapMode.LoRom, recompute: recomputer.Recompute);

        vm.SelectedRomMapMode = RomMapMode.LoRom;

        recomputer.CallCount.Should().Be(0);
    }

    [Fact]
    public void RowsThatVanishFromASnapshotFallBackToThePlaceholder()
    {
        // defensive: a recompute that answers about fewer slots must not leave stale values on
        // screen claiming to have been read at the new mapping.
        var vm = MakeVm(recompute: _ => new SnesVectorSnapshot("T", true, []));

        vm.SelectedRomMapMode = RomMapMode.HiRom;

        vm.Vectors.Should().OnlyContain(row =>
            row.DisplayValue == SnesVectorSnapshot.UnreadablePlaceholder);
    }

    // ------------------------------------------------------------------
    // D2 (PRESERVE): the always-on reserved slots
    // ------------------------------------------------------------------

    [Fact]
    public void D2_TheReservedSlotsAreEnabledAndNotSelectable()
    {
        var vm = MakeVm(initiallyEnabled: []);

        foreach (var name in AlwaysOnVectorNames)
        {
            var row = vm.Vectors.Single(r => r.Name == name);
            row.IsAlwaysEnabled.Should().BeTrue();
            row.IsEnabled.Should().BeTrue();
            row.IsSelectable.Should().BeFalse();
        }
    }

    [Fact]
    public void D2_TheReservedSlotsSurviveEverySelectableVectorBeingSwitchedOff()
    {
        var vm = MakeVm();

        foreach (var row in vm.Vectors)
            row.IsEnabled = false;

        vm.EnabledVectorNames.Should().Equal(AlwaysOnVectorNames,
            "these map to real 65816 vector slots that the SNES leaves unused; labelling them " +
            "documents the table, and there has never been a way to decline them");
    }

    [Fact]
    public void D2_TheReservedSlotsSurviveATotalFailureToReadAnyVector()
    {
        var vm = MakeVm(initial: UnreadableSnapshot());

        vm.EnabledVectorNames.Should().Equal(AlwaysOnVectorNames);
    }

    [Fact]
    public void D2_TheReservedSlotsSurviveASwitchToAMappingNothingCanBeReadAt()
    {
        var vm = MakeVm(recompute: _ => UnreadableSnapshot());

        vm.SelectedRomMapMode = RomMapMode.HiRom;

        vm.EnabledVectorNames.Should().Equal(AlwaysOnVectorNames);
    }

    // ------------------------------------------------------------------
    // what the caller reads back
    // ------------------------------------------------------------------

    [Fact]
    public void EnabledVectorNamesFollowsTheTickBoxesInVectorTableOrder()
    {
        var vm = MakeVm(initiallyEnabled: []);

        vm.Vectors.Single(r => r.Name == EmulationReset).IsEnabled = true;
        vm.Vectors.Single(r => r.Name == NativeCop).IsEnabled = true;

        vm.EnabledVectorNames.Should().Equal(
            NativeReserved1, NativeReserved2, NativeCop,
            EmulationReserved1, EmulationReserved2, EmulationReset);
    }

    [Fact]
    public void OnlyTheInitiallyEnabledSelectableVectorsStartOutTicked()
    {
        var vm = MakeVm(initiallyEnabled: [NativeNmi]);

        vm.EnabledVectorNames.Should().Equal(
            NativeReserved1, NativeReserved2, NativeNmi, EmulationReserved1, EmulationReserved2);
    }

    [Fact]
    public void AVectorCanBeTickedBackOnAfterBeingSwitchedOff()
    {
        var vm = MakeVm();
        var row = vm.Vectors.Single(r => r.Name == NativeIrq);

        row.IsEnabled = false;
        vm.EnabledVectorNames.Should().NotContain(NativeIrq);

        row.IsEnabled = true;
        vm.EnabledVectorNames.Should().Contain(NativeIrq);
    }

    // ------------------------------------------------------------------
    // D5: unreadable vectors say why
    // ------------------------------------------------------------------

    [Fact]
    public void D5_UnreadableVectorsExplainThemselvesInsteadOfJustShowingQuestionMarks()
    {
        var vm = MakeVm(initial: UnreadableSnapshot());

        vm.StatusText.Should().Be(SnesImportRomViewModel.VectorsUnreadableMessage);
        vm.CartridgeTitle.Should().Be(SnesVectorSnapshot.UnreadableCartridgeTitle);
        vm.Vectors.Should().OnlyContain(row =>
            row.DisplayValue == SnesVectorSnapshot.UnreadablePlaceholder);
        vm.Vectors.Should().OnlyContain(row => !row.IsSelectable);
    }

    [Fact]
    public void D5_SwitchingToAMappingTheVectorsCantBeReadAtRaisesTheStatusLine()
    {
        var vm = MakeVm();
        vm.StatusText.Should().BeEmpty();

        vm.SelectedRomMapMode = RomMapMode.HiRom;
        vm.StatusText.Should().BeEmpty("that recompute answered with readable values");

        var vm2 = MakeVm(recompute: _ => UnreadableSnapshot());
        vm2.SelectedRomMapMode = RomMapMode.HiRom;

        vm2.StatusText.Should().Be(SnesImportRomViewModel.VectorsUnreadableMessage);
    }

    [Fact]
    public void D5_TheStatusLineClearsWhenTheVectorsBecomeReadableAgain()
    {
        var readable = false;
        var vm = MakeVm(
            initial: UnreadableSnapshot(),
            recompute: _ => readable ? ReadableSnapshot() : UnreadableSnapshot());

        vm.StatusText.Should().Be(SnesImportRomViewModel.VectorsUnreadableMessage);

        readable = true;
        vm.SelectedRomMapMode = RomMapMode.HiRom;

        vm.StatusText.Should().BeEmpty();
        vm.Vectors.Single(r => r.Name == NativeIrq).IsSelectable.Should().BeTrue();
    }

    [Fact]
    public void AnUnreadableRowCannotBeTickedOn()
    {
        var vm = MakeVm(initial: UnreadableSnapshot());
        var row = vm.Vectors.Single(r => r.Name == NativeIrq);

        row.IsEnabled = true;

        row.IsEnabled.Should().BeFalse("there is nothing at that slot to point a label at");
    }

    [Fact]
    public void ASlotWhoseValueIsNotARomAddressIsNotSelectableEvenThoughTheReadWorked()
    {
        // the whole table read fine, but this one slot points below $8000, so there is no ROM
        // location to label.
        var snapshot = new SnesVectorSnapshot(
            "TEST CART",
            VectorsReadable: true,
            AllVectorNames
                .Select(n => new SnesVectorValue(n, n == NativeIrq ? "0000" : "8000", n != NativeIrq))
                .ToList());

        var vm = MakeVm(initial: snapshot);

        vm.Vectors.Single(r => r.Name == NativeIrq).IsSelectable.Should().BeFalse();
        vm.Vectors.Single(r => r.Name == NativeIrq).IsEnabled.Should().BeFalse();
        vm.Vectors.Single(r => r.Name == NativeNmi).IsSelectable.Should().BeTrue();
        vm.StatusText.Should().BeEmpty("the table itself was readable");
    }

    // ------------------------------------------------------------------
    // confirmation: computed, never asked
    // ------------------------------------------------------------------

    [Fact]
    public void ACleanDetectionThatTheUserAcceptsNeedsNoConfirmation()
    {
        var vm = MakeVm(detected: RomMapMode.LoRom, detectionSucceeded: true);

        vm.RequiresConfirmation.Should().BeFalse();
        vm.ConfirmationMessage.Should().BeNull();
    }

    [Fact]
    public void AFailedDetectionNeedsConfirmation()
    {
        var vm = MakeVm(detectionSucceeded: false);

        vm.RequiresConfirmation.Should().BeTrue();
        vm.ConfirmationMessage.Should().Be(SnesImportRomViewModel.DetectionFailedConfirmationMessage);
    }

    [Fact]
    public void OverrulingADetectedMappingNeedsConfirmation()
    {
        var vm = MakeVm(detected: RomMapMode.LoRom, detectionSucceeded: true);

        vm.SelectedRomMapMode = RomMapMode.HiRom;

        vm.RequiresConfirmation.Should().BeTrue();
        vm.ConfirmationMessage.Should().Be(SnesImportRomViewModel.OverriddenMapModeConfirmationMessage);
    }

    [Fact]
    public void PuttingAnOverriddenMappingBackClearsTheConfirmation()
    {
        var vm = MakeVm(detected: RomMapMode.LoRom, detectionSucceeded: true);

        vm.SelectedRomMapMode = RomMapMode.HiRom;
        vm.SelectedRomMapMode = RomMapMode.LoRom;

        vm.RequiresConfirmation.Should().BeFalse();
        vm.ConfirmationMessage.Should().BeNull();
    }

    [Fact]
    public void TheTwoWarningsAreMutuallyExclusive()
    {
        // detection failed AND the selection differs from the (meaningless) detected value: only
        // the failure is worth saying, because there is no detected mapping to differ from.
        var vm = MakeVm(detected: RomMapMode.LoRom, detectionSucceeded: false);

        vm.SelectedRomMapMode = RomMapMode.HiRom;

        vm.ConfirmationMessage.Should()
            .Be(SnesImportRomViewModel.DetectionFailedConfirmationMessage)
            .And.NotBe(SnesImportRomViewModel.OverriddenMapModeConfirmationMessage);
    }

    // ------------------------------------------------------------------
    // the two synthesis switches
    // ------------------------------------------------------------------

    [Fact]
    public void D4_BothSynthesisSwitchesStartOn()
    {
        var vm = MakeVm();

        vm.GenerateHeaderFlags.Should().BeTrue();
        vm.GenerateBankRegions.Should().BeTrue("importing has always done this; the switch only allows declining it");
    }

    [Fact]
    public void TheSynthesisSwitchesCanBeTurnedOff()
    {
        var vm = MakeVm();

        vm.GenerateHeaderFlags = false;
        vm.GenerateBankRegions = false;

        vm.GenerateHeaderFlags.Should().BeFalse();
        vm.GenerateBankRegions.Should().BeFalse();
    }

    [Fact]
    public void ChangingTheMapModeLeavesTheSynthesisSwitchesAlone()
    {
        var vm = MakeVm();
        vm.GenerateBankRegions = false;

        vm.SelectedRomMapMode = RomMapMode.HiRom;

        vm.GenerateBankRegions.Should().BeFalse();
        vm.GenerateHeaderFlags.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // change notification
    // ------------------------------------------------------------------

    [Fact]
    public void EverySettableViewModelPropertyAnnouncesItself()
    {
        var vm = MakeVm();
        var raised = RecordNotifications(vm);

        vm.SelectedRomMapMode = RomMapMode.HiRom;
        vm.GenerateHeaderFlags = false;
        vm.GenerateBankRegions = false;

        raised.Should().Contain(nameof(SnesImportRomViewModel.SelectedRomMapMode));
        raised.Should().Contain(nameof(SnesImportRomViewModel.GenerateHeaderFlags));
        raised.Should().Contain(nameof(SnesImportRomViewModel.GenerateBankRegions));
    }

    [Fact]
    public void ChangingTheMapModeAnnouncesEverythingDerivedFromIt()
    {
        var vm = MakeVm(recompute: _ => UnreadableSnapshot());
        var raised = RecordNotifications(vm);

        vm.SelectedRomMapMode = RomMapMode.HiRom;

        raised.Should().Contain(nameof(SnesImportRomViewModel.CartridgeTitle));
        raised.Should().Contain(nameof(SnesImportRomViewModel.StatusText));
        raised.Should().Contain(nameof(SnesImportRomViewModel.RequiresConfirmation));
        raised.Should().Contain(nameof(SnesImportRomViewModel.ConfirmationMessage));
    }

    [Fact]
    public void RowsAnnounceTheirOwnChanges()
    {
        var vm = MakeVm(recompute: _ => UnreadableSnapshot());
        var row = vm.Vectors.Single(r => r.Name == NativeIrq);
        var raised = new List<string>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        row.IsEnabled = false;
        vm.SelectedRomMapMode = RomMapMode.HiRom;

        raised.Should().Contain(nameof(SnesVectorRowViewModel.IsEnabled));
        raised.Should().Contain(nameof(SnesVectorRowViewModel.DisplayValue));
        raised.Should().Contain(nameof(SnesVectorRowViewModel.IsSelectable));
    }

    [Fact]
    public void EveryNotificationGoesThroughTheMarshaller()
    {
        // the thread rule: a host's marshaller is the only path to the UI thread, and the rows
        // share the parent's, so one hop covers the whole tree.
        var queued = new List<Action>();
        var vm = MakeVm(marshaller: queued.Add, recompute: _ => UnreadableSnapshot());
        var raised = RecordNotifications(vm);
        var rowRaised = new List<string>();
        vm.Vectors[2].PropertyChanged += (_, e) => rowRaised.Add(e.PropertyName!);

        vm.SelectedRomMapMode = RomMapMode.HiRom;
        vm.GenerateBankRegions = false;

        raised.Should().BeEmpty("nothing may bypass the marshaller");
        rowRaised.Should().BeEmpty("the rows were handed the same marshaller, not the default one");
        queued.Should().NotBeEmpty();

        foreach (var queuedAction in queued)
            queuedAction();

        raised.Should().Contain(nameof(SnesImportRomViewModel.SelectedRomMapMode));
        raised.Should().Contain(nameof(SnesImportRomViewModel.GenerateBankRegions));
        rowRaised.Should().Contain(nameof(SnesVectorRowViewModel.DisplayValue));
    }

    [Fact]
    public void StateIsReadableBeforeADeferredMarshallerHasRunAnything()
    {
        var queued = new List<Action>();
        var vm = MakeVm(marshaller: queued.Add, recompute: _ => ReadableSnapshot("LATER TITLE"));

        vm.SelectedRomMapMode = RomMapMode.HiRom;

        vm.CartridgeTitle.Should().Be("LATER TITLE", "the state changes immediately; only the telling is deferred");
        queued.Should().NotBeEmpty();
    }
}
