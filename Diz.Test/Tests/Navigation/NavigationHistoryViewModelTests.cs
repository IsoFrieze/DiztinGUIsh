using System;
using System.Collections.Generic;
using System.ComponentModel;
using Diz.Core.model;
using Diz.Ui.ViewModels.Navigation;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.Navigation;

/// <summary>
/// NavigationHistoryViewModel: the back/forward stack, as testable state.
///
/// What these tests pin down, in order of how easy it would be to break:
///
/// 1. RECORDING A POINT DOES NOT NAVIGATE. The old WinForms control routed both "a point was
///    recorded" and "the user picked a row" through one BindingSource.Position, so every append
///    fired a navigation to the entry that had just been recorded. The ViewModel keeps those two
///    apart, and several tests here exist only to stop them being re-joined.
///
/// 2. CLAMP, NEVER WRAP. Back at the oldest entry stays there -- and still asks to navigate to it,
///    which is not the same as doing nothing, and is the behaviour the control had.
///
/// 3. OVERSHOOT IS SAID OUT LOUD by whoever asks to move, and comes back out unchanged in the
///    request. The asymmetry it preserves (menu back/forward overshoot, row-click does not) is the
///    host's business; the ViewModel just carries the number.
///
/// The address converter is a delegate everywhere here for the same reason it is one in
/// production: the real one hangs off whichever project happens to be open, and the ViewModel
/// outlives any particular project.
/// </summary>
public class NavigationHistoryViewModelTests
{
    /// <summary>What the WinForms host passes for menu-driven back/forward (MainWindow.Actions).</summary>
    private const int MenuOvershoot = 12;

    /// <summary>SNES addresses in these tests convert to a ROM offset by dropping this.</summary>
    private const int SnesBase = 0xC00000;

    private static int Convert(int snesAddress) =>
        snesAddress >= SnesBase ? snesAddress - SnesBase : -1;

    private static BindingList<NavigationEntry> HistoryList(params int[] snesAddresses)
    {
        // built exactly the way DizDocument builds it, flags and all -- AllowRemove: false is
        // load-bearing for the Clear() tests.
        var list = new BindingList<NavigationEntry>
        {
            RaiseListChangedEvents = true,
            AllowNew = false,
            AllowRemove = false,
            AllowEdit = false,
        };

        foreach (var snesAddress in snesAddresses)
            list.Add(Entry(snesAddress));

        return list;
    }

    private static NavigationEntry Entry(int snesAddress, string description = "went somewhere") =>
        new(snesAddress, description, "start", data: null);

    /// <summary>A ViewModel plus every navigation it asked for, in order.</summary>
    private static (NavigationHistoryViewModel vm, List<NavigationRequest> requests) Build(
        BindingList<NavigationEntry> history,
        Func<int, int> converter = null)
    {
        var vm = new NavigationHistoryViewModel(history, converter ?? Convert);
        var requests = new List<NavigationRequest>();
        vm.NavigationRequested += (_, request) => requests.Add(request);
        return (vm, requests);
    }

    // ------------------------------------------------------------------
    // construction
    // ------------------------------------------------------------------

    [Fact]
    public void AViewModelBuiltOverAnEmptyHistoryHasNothingSelected()
    {
        var (vm, requests) = Build(HistoryList());

        vm.Count.Should().Be(0);
        vm.CurrentIndex.Should().Be(NavigationHistoryViewModel.NoSelection);
        vm.CurrentEntry.Should().BeNull();
        requests.Should().BeEmpty();
    }

    [Fact]
    public void AViewModelBuiltOverAnExistingHistoryStartsOnTheNewestEntryWithoutNavigating()
    {
        var (vm, requests) = Build(HistoryList(SnesBase + 0x10, SnesBase + 0x20, SnesBase + 0x30));

        vm.CurrentIndex.Should().Be(2, "an append would have left it there; adopting a list is no different");
        vm.CurrentEntry.SnesOffset.Should().Be(SnesBase + 0x30);
        requests.Should().BeEmpty("constructing a ViewModel is not a navigation");
    }

    [Fact]
    public void ConstructionRefusesAMissingHistoryOrConverter()
    {
        var act1 = () => new NavigationHistoryViewModel(null!, Convert);
        var act2 = () => new NavigationHistoryViewModel(HistoryList(), null!);

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------
    // recording a point must not navigate (the bug this conversion fixes)
    // ------------------------------------------------------------------

    [Fact]
    public void AppendingToTheHistoryMovesOntoTheNewEntryAndRequestsNothing()
    {
        var history = HistoryList(SnesBase + 0x10);
        var (vm, requests) = Build(history);

        history.Add(Entry(SnesBase + 0x20));
        history.Add(Entry(SnesBase + 0x30));

        vm.CurrentIndex.Should().Be(2, "the next 'back' has to leave the point just recorded");
        requests.Should().BeEmpty(
            "recording where the user was is not a reason to send them anywhere; the old control " +
            "re-navigated to every recorded point with overshoot 0");
    }

    [Fact]
    public void TheFirstEverAppendAlsoRequestsNothing()
    {
        // called out separately: this is the one append where the WinForms BindingSource moved its
        // own position (-1 -> 0) without anybody asking it to, so it is the easiest case to
        // re-break.
        var history = HistoryList();
        var (vm, requests) = Build(history);

        history.Add(Entry(SnesBase + 0x10));

        vm.CurrentIndex.Should().Be(0);
        requests.Should().BeEmpty();
    }

    [Fact]
    public void GoingBackThenRecordingANewPointLeavesTheUserOnTheNewPoint()
    {
        var history = HistoryList(SnesBase + 0x10, SnesBase + 0x20, SnesBase + 0x30);
        var (vm, requests) = Build(history);

        vm.MoveBack(MenuOvershoot);
        vm.MoveBack(MenuOvershoot);
        vm.CurrentIndex.Should().Be(0);

        history.Add(Entry(SnesBase + 0x40));

        vm.CurrentIndex.Should().Be(3);
        requests.Should().HaveCount(2, "the append added no navigation of its own");
    }

    // ------------------------------------------------------------------
    // back / forward
    // ------------------------------------------------------------------

    [Fact]
    public void MovingBackGoesOneEntryOlderAndAsksToNavigateThere()
    {
        var (vm, requests) = Build(HistoryList(SnesBase + 0x10, SnesBase + 0x20, SnesBase + 0x30));

        vm.MoveBack(MenuOvershoot);

        vm.CurrentIndex.Should().Be(1);
        requests.Should().ContainSingle().Which.Should()
            .Be(new NavigationRequest(0x20, MenuOvershoot));
    }

    [Fact]
    public void MovingForwardGoesOneEntryNewerAndAsksToNavigateThere()
    {
        var (vm, requests) = Build(HistoryList(SnesBase + 0x10, SnesBase + 0x20, SnesBase + 0x30));
        vm.MoveBack(MenuOvershoot);
        vm.MoveBack(MenuOvershoot);
        requests.Clear();

        vm.MoveForward(MenuOvershoot);

        vm.CurrentIndex.Should().Be(1);
        requests.Should().ContainSingle().Which.Should()
            .Be(new NavigationRequest(0x20, MenuOvershoot));
    }

    [Fact]
    public void BackAtTheOldestEntryStaysThereAndAsksForItAgain()
    {
        var (vm, requests) = Build(HistoryList(SnesBase + 0x10, SnesBase + 0x20));
        vm.MoveBack(MenuOvershoot);
        vm.CurrentIndex.Should().Be(0);
        requests.Clear();

        vm.MoveBack(MenuOvershoot);

        vm.CurrentIndex.Should().Be(0, "clamped, never wrapped round to the newest entry");
        requests.Should().ContainSingle().Which.Should()
            .Be(new NavigationRequest(0x10, MenuOvershoot),
                "the old control re-navigated to the clamped entry rather than doing nothing");
    }

    [Fact]
    public void ForwardAtTheNewestEntryStaysThereAndAsksForItAgain()
    {
        var (vm, requests) = Build(HistoryList(SnesBase + 0x10, SnesBase + 0x20));
        vm.CurrentIndex.Should().Be(1);

        vm.MoveForward(MenuOvershoot);

        vm.CurrentIndex.Should().Be(1, "clamped, never wrapped round to the oldest entry");
        requests.Should().ContainSingle().Which.Should()
            .Be(new NavigationRequest(0x20, MenuOvershoot));
    }

    [Fact]
    public void BackAndForwardDoNothingAtAllWhenThereIsNoHistory()
    {
        var (vm, requests) = Build(HistoryList());

        vm.MoveBack(MenuOvershoot);
        vm.MoveForward(MenuOvershoot);

        vm.CurrentIndex.Should().Be(NavigationHistoryViewModel.NoSelection);
        requests.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // the user picking a row
    // ------------------------------------------------------------------

    [Fact]
    public void SelectingAnEntryMovesOntoItAndAsksToNavigateThere()
    {
        var (vm, requests) = Build(HistoryList(SnesBase + 0x10, SnesBase + 0x20, SnesBase + 0x30));

        vm.SelectEntry(0, NavigationHistoryViewModel.NoOvershoot);

        vm.CurrentIndex.Should().Be(0);
        requests.Should().ContainSingle().Which.Should()
            .Be(new NavigationRequest(0x10, NavigationHistoryViewModel.NoOvershoot));
    }

    [Fact]
    public void SelectingAnEntryThatIsNotThereIsIgnored()
    {
        var (vm, requests) = Build(HistoryList(SnesBase + 0x10, SnesBase + 0x20));

        vm.SelectEntry(-1, NavigationHistoryViewModel.NoOvershoot);
        vm.SelectEntry(2, NavigationHistoryViewModel.NoOvershoot);

        vm.CurrentIndex.Should().Be(1, "unchanged");
        requests.Should().BeEmpty();
    }

    [Fact]
    public void BackAndForwardCarryOnFromWhereTheUserClicked()
    {
        var (vm, requests) = Build(HistoryList(SnesBase + 0x10, SnesBase + 0x20, SnesBase + 0x30));

        vm.SelectEntry(2, NavigationHistoryViewModel.NoOvershoot);
        requests.Clear();

        vm.MoveBack(MenuOvershoot);

        vm.CurrentIndex.Should().Be(1);
        requests.Should().ContainSingle().Which.PcOffset.Should().Be(0x20);
    }

    // ------------------------------------------------------------------
    // overshoot: carried verbatim, per path (see D4 in the conversion plan)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(999)]
    public void WhateverOvershootTheCallerNamesIsWhatComesBackOut(int overshoot)
    {
        var (vm, requests) = Build(HistoryList(SnesBase + 0x10, SnesBase + 0x20));

        vm.MoveBack(overshoot);
        vm.MoveForward(overshoot);
        vm.SelectEntry(0, overshoot);

        requests.Should().HaveCount(3);
        requests.Should().OnlyContain(r => r.OvershootAmount == overshoot);
    }

    [Fact]
    public void TheTwoHostPathsMayNameDifferentOvershootsInTheSameSession()
    {
        var (vm, requests) = Build(HistoryList(SnesBase + 0x10, SnesBase + 0x20));

        vm.MoveBack(MenuOvershoot);
        vm.SelectEntry(1, NavigationHistoryViewModel.NoOvershoot);

        requests.Should().Equal(
            new NavigationRequest(0x10, MenuOvershoot),
            new NavigationRequest(0x20, NavigationHistoryViewModel.NoOvershoot));
    }

    // ------------------------------------------------------------------
    // entries that name nowhere
    // ------------------------------------------------------------------

    [Fact]
    public void AnEntryWithNoRealSnesAddressMovesTheSelectionButRequestsNothing()
    {
        var history = HistoryList(SnesBase + 0x10);
        history.Add(Entry(-1));
        history.Add(Entry(SnesBase + 0x30));
        var (vm, requests) = Build(history);

        vm.MoveBack(MenuOvershoot);

        vm.CurrentIndex.Should().Be(1,
            "the selection still moves, or back/forward could never step past a dead entry");
        requests.Should().BeEmpty();
    }

    [Fact]
    public void AnAddressThatIsNotInTheOpenRomRequestsNothing()
    {
        // history outlives a project: entries recorded against one ROM can be un-convertible in
        // the next. The converter answers -1, exactly as Data.ConvertSnesToPc does.
        var (vm, requests) = Build(
            HistoryList(SnesBase + 0x10, SnesBase + 0x20),
            converter: _ => -1);

        vm.MoveBack(MenuOvershoot);

        vm.CurrentIndex.Should().Be(0);
        requests.Should().BeEmpty();
    }

    [Fact]
    public void SteppingPastADeadEntryReachesTheLiveOneBehindIt()
    {
        var history = HistoryList(SnesBase + 0x10);
        history.Add(Entry(-1));
        history.Add(Entry(SnesBase + 0x30));
        var (vm, requests) = Build(history);

        vm.MoveBack(MenuOvershoot);
        vm.MoveBack(MenuOvershoot);

        vm.CurrentIndex.Should().Be(0);
        requests.Should().ContainSingle().Which.PcOffset.Should().Be(0x10);
    }

    // ------------------------------------------------------------------
    // clearing
    // ------------------------------------------------------------------

    [Fact]
    public void ClearEmptiesTheHistoryEvenThoughTheListForbidsRemoval()
    {
        // AllowRemove: false guards RemoveItem, not ClearItems. Asserted rather than assumed,
        // because the whole Clear History button depends on it.
        var history = HistoryList(SnesBase + 0x10, SnesBase + 0x20);
        history.AllowRemove.Should().BeFalse("this is how DizDocument builds the list");
        var (vm, requests) = Build(history);

        vm.Clear();

        history.Should().BeEmpty();
        vm.Count.Should().Be(0);
        vm.CurrentIndex.Should().Be(NavigationHistoryViewModel.NoSelection);
        vm.CurrentEntry.Should().BeNull();
        requests.Should().BeEmpty("throwing history away does not move the user");
    }

    [Fact]
    public void BackAndForwardDoNothingAfterAClear()
    {
        var (vm, requests) = Build(HistoryList(SnesBase + 0x10, SnesBase + 0x20));
        vm.Clear();

        vm.MoveBack(MenuOvershoot);
        vm.MoveForward(MenuOvershoot);

        requests.Should().BeEmpty();
        vm.CurrentIndex.Should().Be(NavigationHistoryViewModel.NoSelection);
    }

    [Fact]
    public void RecordingAgainAfterAClearStartsTheHistoryOver()
    {
        var history = HistoryList(SnesBase + 0x10, SnesBase + 0x20);
        var (vm, requests) = Build(history);
        vm.Clear();

        history.Add(Entry(SnesBase + 0x40));

        vm.Count.Should().Be(1);
        vm.CurrentIndex.Should().Be(0);
        requests.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // notifications
    // ------------------------------------------------------------------

    [Fact]
    public void MovingAnnouncesTheNewPositionAndEntry()
    {
        var (vm, _) = Build(HistoryList(SnesBase + 0x10, SnesBase + 0x20));
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.MoveBack(MenuOvershoot);

        raised.Should().Contain(nameof(NavigationHistoryViewModel.CurrentIndex));
        raised.Should().Contain(nameof(NavigationHistoryViewModel.CurrentEntry));
    }

    [Fact]
    public void StayingPutAnnouncesNothing()
    {
        var (vm, _) = Build(HistoryList(SnesBase + 0x10));
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.MoveBack(MenuOvershoot);

        raised.Should().BeEmpty("the clamp put it back where it already was");
    }

    [Fact]
    public void ADisposedViewModelStopsFollowingTheHistory()
    {
        var history = HistoryList(SnesBase + 0x10);
        var (vm, _) = Build(history);

        vm.Dispose();
        history.Add(Entry(SnesBase + 0x20));

        vm.CurrentIndex.Should().Be(0, "no longer listening");
    }
}
