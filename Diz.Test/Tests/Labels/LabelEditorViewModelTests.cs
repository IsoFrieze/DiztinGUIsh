using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Diz.Core.model;
using Diz.Ui.ViewModels.Labels;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.Labels;

/// <summary>
/// Step 2 (new-ui-plan.md): LabelEditorViewModel against the REAL provider
/// (LabelsServiceWithTemp -- the same class the app uses), no mocks for the model side.
/// </summary>
public class LabelEditorViewModelTests
{
    // LabelsServiceWithTemp's ctor only stores Data (used by temp-label paths we don't touch);
    // same construction the Step 0 event tests use.
    private static LabelsServiceWithTemp NewProvider(
        params (int addr, string name, string comment)[] entries)
    {
        var svc = new LabelsServiceWithTemp(null!);
        foreach (var (addr, name, comment) in entries)
            svc.AddLabel(addr, new Label { Name = name, Comment = comment });
        return svc;
    }

    private static LabelEditorViewModel NewVm(
        LabelsServiceWithTemp provider,
        Action<Action>? marshaller = null,
        NormalizeWramLabelsPort? normalizeWram = null,
        ResolveRomOffsetToSnesIaPort? resolveIa = null) =>
        new(provider, marshaller, normalizeWram, resolveIa);

    private static List<NotifyCollectionChangedEventArgs> RecordCollectionChanges(
        ReadOnlyObservableCollection<ILabelRowViewModel> rows)
    {
        var changes = new List<NotifyCollectionChangedEventArgs>();
        ((INotifyCollectionChanged)rows).CollectionChanged += (_, e) => changes.Add(e);
        return changes;
    }

    // =====================================================================================
    // row pipeline: incremental updates per LabelChangeKind
    // =====================================================================================

    [Fact]
    public void Ctor_PopulatesRowsFromProvider_SortedByAddressAscending()
    {
        using var vm = NewVm(NewProvider(
            (0x7E0100, "player_hp", ""),
            (0x808000, "reset_vector", "entry"),
            (0x018000, "some_code", "")));

        vm.Rows.Select(r => r.SnesAddress).Should().Equal(0x018000, 0x7E0100, 0x808000);
        vm.TotalLabelCount.Should().Be(3);
        vm.VisibleLabelCount.Should().Be(3);
        vm.Rows[2].AddressText.Should().Be("808000");
        vm.Rows[2].Name.Should().Be("reset_vector");
    }

    [Fact]
    public void ProviderAdd_InsertsExactlyOneRow_AtSortedPosition_NotAFullReset()
    {
        var provider = NewProvider((0x010000, "a", ""), (0x030000, "c", ""));
        using var vm = NewVm(provider);
        var changes = RecordCollectionChanges(vm.Rows);

        provider.AddLabel(0x020000, new Label { Name = "b" });

        changes.Should().ContainSingle().Which.Action.Should().Be(NotifyCollectionChangedAction.Add);
        vm.Rows.Select(r => r.Name).Should().Equal("a", "b", "c");
        vm.TotalLabelCount.Should().Be(3);
    }

    [Fact]
    public void ProviderRemove_RemovesExactlyOneRow()
    {
        var provider = NewProvider((0x010000, "a", ""), (0x020000, "b", ""));
        using var vm = NewVm(provider);
        var changes = RecordCollectionChanges(vm.Rows);

        provider.RemoveLabel(0x010000);

        changes.Should().ContainSingle().Which.Action.Should().Be(NotifyCollectionChangedAction.Remove);
        vm.Rows.Should().ContainSingle().Which.Name.Should().Be("b");
        vm.TotalLabelCount.Should().Be(1);
    }

    [Fact]
    public void ProviderReplace_RebindsExistingRowInstance()
    {
        var provider = NewProvider((0x010000, "old_name", "old comment"));
        using var vm = NewVm(provider);
        var rowBefore = vm.Rows.Single();

        provider.AddLabel(0x010000, new Label { Name = "new_name", Comment = "new" }, overwrite: true);

        vm.Rows.Should().ContainSingle();
        vm.Rows.Single().Should().BeSameAs(rowBefore, "a Replaced event rebinds, not recreates");
        rowBefore.Name.Should().Be("new_name");
        rowBefore.Comment.Should().Be("new");
    }

    /// <summary>
    /// The provider quirk documented in the plan: AddLabel with overwrite:false on an
    /// occupied address KEEPS the old label but still reports Replaced. The pipeline must
    /// re-read the provider and end up correct, not blindly apply the event.
    /// </summary>
    [Fact]
    public void OverReportedReplaced_IsHandledIdempotently()
    {
        var provider = NewProvider((0x010000, "keeper", ""));
        using var vm = NewVm(provider);

        provider.AddLabel(0x010000, new Label { Name = "intruder" }, overwrite: false);

        vm.TotalLabelCount.Should().Be(1);
        vm.Rows.Single().Name.Should().Be("keeper", "the provider kept the original label");
    }

    [Fact]
    public void ProviderBulkReset_RebuildsAllRows()
    {
        var provider = NewProvider((0x010000, "a", ""));
        using var vm = NewVm(provider);

        provider.SetAll(new System.Collections.Generic.Dictionary<int, Diz.Core.Interfaces.IAnnotationLabel>
        {
            [0x020000] = new Label { Name = "x" },
            [0x030000] = new Label { Name = "y" },
        });

        vm.Rows.Select(r => r.Name).Should().Equal("x", "y");
        vm.TotalLabelCount.Should().Be(2);
    }

    /// <summary>
    /// Step 0 documented that an in-place rename fires Label.PropertyChanged but NOT the
    /// provider's LabelsChanged. The row relays the label's own INPC, closing that gap.
    /// </summary>
    [Fact]
    public void InPlaceRenameOnModelLabel_UpdatesRow_WithoutAnyProviderEvent()
    {
        var provider = NewProvider((0x010000, "before", ""));
        using var vm = NewVm(provider);
        var row = vm.Rows.Single();
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        ((Label)provider.GetLabel(0x010000)!).Name = "after";

        row.Name.Should().Be("after");
        raised.Should().Contain(nameof(ILabelRowViewModel.Name));
    }

    // =====================================================================================
    // filter + sort
    // =====================================================================================

    private static LabelsServiceWithTemp FilterFixture() => NewProvider(
        (0x7E0100, "player_hp", "hit points"),
        (0x7E0102, "player_mp", "magic"),
        (0x808000, "reset_vector", "entry point"),
        (0xC00000, "data_table", "hit table"));

    [Fact]
    public void SearchTerm_FiltersByName_CaseInsensitive()
    {
        using var vm = NewVm(FilterFixture());
        vm.SearchTerm = "PLAYER";

        vm.Rows.Select(r => r.Name).Should().Equal("player_hp", "player_mp");
        vm.VisibleLabelCount.Should().Be(2);
        vm.TotalLabelCount.Should().Be(4, "the filter hides rows, it does not delete labels");
    }

    [Fact]
    public void SearchTerm_FiltersByComment_AndByHexAddressText()
    {
        using var vm = NewVm(FilterFixture());

        vm.SearchTerm = "entry";
        vm.Rows.Single().Name.Should().Be("reset_vector");

        vm.SearchTerm = "7E01";
        vm.Rows.Select(r => r.Name).Should().Equal("player_hp", "player_mp");
    }

    [Fact]
    public void SearchTerm_MultipleTerms_AllMustMatch()
    {
        using var vm = NewVm(FilterFixture());
        vm.SearchTerm = "hit player";
        vm.Rows.Single().Name.Should().Be("player_hp");
    }

    [Fact]
    public void SearchTerm_IsRam_And_AddressComparisons_AreSupported()
    {
        using var vm = NewVm(FilterFixture());

        vm.SearchTerm = "is:ram";
        vm.Rows.Select(r => r.Name).Should().Equal("player_hp", "player_mp");

        vm.SearchTerm = ">7E0100";
        vm.Rows.Select(r => r.SnesAddress).Should().Equal(0x7E0102, 0x808000, 0xC00000);
    }

    [Fact]
    public void SearchTerm_MatchesContextMappingNameOverrides()
    {
        var provider = NewProvider((0x7E0050, "tmp50", ""));
        ((Label)provider.GetLabel(0x7E0050)!).ContextMappings.Add(
            new ContextMapping { Context = "Battle", NameOverride = "player_special_hp" });
        using var vm = NewVm(provider);

        vm.SearchTerm = "special";
        vm.Rows.Should().ContainSingle();
    }

    [Fact]
    public void ClearSearch_RestoresAllRows()
    {
        using var vm = NewVm(FilterFixture());
        vm.SearchTerm = "player";
        vm.VisibleLabelCount.Should().Be(2);

        vm.ClearSearch();

        vm.SearchTerm.Should().BeEmpty();
        vm.VisibleLabelCount.Should().Be(4);
    }

    [Fact]
    public void SortByName_AscendingAndDescending()
    {
        using var vm = NewVm(FilterFixture());

        vm.SortField = LabelField.Name;
        vm.Rows.Select(r => r.Name).Should().Equal("data_table", "player_hp", "player_mp", "reset_vector");

        vm.SortDescending = true;
        vm.Rows.Select(r => r.Name).Should().Equal("reset_vector", "player_mp", "player_hp", "data_table");
    }

    [Fact]
    public void SortByComment_TiesBreakByAddressAscending()
    {
        using var vm = NewVm(NewProvider(
            (0x030000, "c", "same"),
            (0x010000, "a", "same"),
            (0x020000, "b", "same")));

        vm.SortField = LabelField.Comment;
        vm.Rows.Select(r => r.SnesAddress).Should().Equal(0x010000, 0x020000, 0x030000);
    }

    [Fact]
    public void SortDescendingOnAddress_ReversesRows()
    {
        using var vm = NewVm(FilterFixture());
        vm.SortDescending = true;
        vm.Rows.Select(r => r.SnesAddress).Should().BeInDescendingOrder();
    }

    [Fact]
    public void ProviderAdd_WhileFilteredAndSorted_LandsInTheRightPlaceOrIsHidden()
    {
        var provider = FilterFixture();
        using var vm = NewVm(provider);
        vm.SearchTerm = "player";

        // hidden by the filter:
        provider.AddLabel(0xD00000, new Label { Name = "unrelated" });
        vm.VisibleLabelCount.Should().Be(2);
        vm.TotalLabelCount.Should().Be(5);

        // matches the filter -> appears, sorted by address between the two existing ones:
        provider.AddLabel(0x7E0101, new Label { Name = "player_shield" });
        vm.Rows.Select(r => r.Name).Should().Equal("player_hp", "player_shield", "player_mp");
    }

    // =====================================================================================
    // validation: ValidateEdit / CommitEdit
    // =====================================================================================

    [Theory]
    [InlineData("", true)]              // empty names are load-bearing (23 in the CT corpus)
    [InlineData("+", true)]             // asar anonymous branch
    [InlineData("--", true)]            // asar anonymous branch (run of same char)
    [InlineData("valid_name", true)]
    [InlineData(".start_animation", true)]  // leading-dot sublabel, real corpus name
    [InlineData("status_process_2.5x_evade", true)] // dotted segments may start with a digit
    [InlineData("9foo", false)]         // leading digit: asar E5059
    [InlineData("foo-bar", false)]      // '-' not a name char in asar: E5062 (Strict only)
    [InlineData("foo bar", false)]      // space never legal
    [InlineData("+-", false)]           // mixed anonymous run
    public void ValidateEdit_Name_UsesStrictRules(string proposed, bool expectValid)
    {
        using var vm = NewVm(NewProvider((0x010000, "x", "")));
        var row = vm.Rows.Single();

        vm.ValidateEdit(row, LabelField.Name, proposed).IsValid.Should().Be(expectValid);
    }

    [Fact]
    public void ValidateEdit_Address_RejectsNonHex_WithTheHistoricalMessage()
    {
        using var vm = NewVm(NewProvider((0x010000, "x", "")));
        var row = vm.Rows.Single();

        var result = vm.ValidateEdit(row, LabelField.Address, "not hex");
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Must enter a valid hex address.");
    }

    /// <summary>
    /// DELIBERATE FIX over WinForms (documented in the plan): the old duplicate-address guard
    /// was dead code (its `existingSnesAddress == -1` condition was unreachable because
    /// int.TryParse writes 0 on failure), so moving a row onto an occupied address silently
    /// overwrote that label. The VM revives the guard with the original message.
    /// </summary>
    [Fact]
    public void ValidateEdit_Address_RejectsOccupiedAddress_ButAcceptsOwnAddress()
    {
        using var vm = NewVm(NewProvider((0x010000, "a", ""), (0x020000, "b", "")));
        var rowA = vm.Rows.First(r => r.SnesAddress == 0x010000);

        var occupied = vm.ValidateEdit(rowA, LabelField.Address, "020000");
        occupied.IsValid.Should().BeFalse();
        occupied.Error.Should().Be("This address already has a label.");

        vm.ValidateEdit(rowA, LabelField.Address, "010000").IsValid.Should().BeTrue("no-op re-entry of own address");
        vm.ValidateEdit(rowA, LabelField.Address, "7E5000").IsValid.Should().BeTrue();
    }

    [Fact]
    public void CommitEdit_Name_WritesThroughToProvider_AndClearsStatus()
    {
        var provider = NewProvider((0x010000, "before", "keep me"));
        using var vm = NewVm(provider);
        var row = vm.Rows.Single();

        var result = vm.CommitEdit(row, LabelField.Name, "after");

        result.IsValid.Should().BeTrue();
        vm.StatusText.Should().BeEmpty();
        provider.GetLabel(0x010000)!.Name.Should().Be("after");
        provider.GetLabel(0x010000)!.Comment.Should().Be("keep me");
        vm.Rows.Single().Name.Should().Be("after");
        vm.TotalLabelCount.Should().Be(1);
    }

    [Fact]
    public void CommitEdit_InvalidName_LeavesProviderUntouched_AndSetsStatusText()
    {
        var provider = NewProvider((0x010000, "before", ""));
        using var vm = NewVm(provider);

        var result = vm.CommitEdit(vm.Rows.Single(), LabelField.Name, "9bad");

        result.IsValid.Should().BeFalse();
        vm.StatusText.Should().NotBeEmpty();
        provider.GetLabel(0x010000)!.Name.Should().Be("before");
    }

    [Fact]
    public void CommitEdit_Comment_WritesThrough()
    {
        var provider = NewProvider((0x010000, "n", "old"));
        using var vm = NewVm(provider);

        vm.CommitEdit(vm.Rows.Single(), LabelField.Comment, "new comment").IsValid.Should().BeTrue();
        provider.GetLabel(0x010000)!.Comment.Should().Be("new comment");
    }

    /// <summary>Address edits change row identity: remove+add on the provider.</summary>
    [Fact]
    public void CommitEdit_Address_MovesLabel_RowIdentityChanges_SelectionFollows()
    {
        var provider = NewProvider((0x010000, "mover", "c"));
        using var vm = NewVm(provider);
        var oldRow = vm.Rows.Single();
        vm.SelectedRow = oldRow;

        var result = vm.CommitEdit(oldRow, LabelField.Address, "7E2000");

        result.IsValid.Should().BeTrue();
        provider.GetLabel(0x010000).Should().BeNull();
        provider.GetLabel(0x7E2000)!.Name.Should().Be("mover");

        vm.Rows.Should().ContainSingle();
        var newRow = vm.Rows.Single();
        newRow.Should().NotBeSameAs(oldRow, "the address IS row identity");
        newRow.SnesAddress.Should().Be(0x7E2000);
        vm.SelectedRow.Should().BeSameAs(newRow, "selection follows the moved label");
    }

    /// <summary>WinForms parity: a commit carries the old label's context mappings over
    /// (by reference) into the fresh Label it writes.</summary>
    [Fact]
    public void CommitEdit_CarriesContextMappingsOver()
    {
        var provider = NewProvider((0x010000, "n", ""));
        ((Label)provider.GetLabel(0x010000)!).ContextMappings.Add(
            new ContextMapping { Context = "Battle", NameOverride = "other" });
        using var vm = NewVm(provider);

        vm.CommitEdit(vm.Rows.Single(), LabelField.Name, "renamed");

        var after = provider.GetLabel(0x010000)!;
        after.Name.Should().Be("renamed");
        after.ContextMappings.Should().ContainSingle()
            .Which.NameOverride.Should().Be("other");
        vm.Rows.Single().ContextSummary.Should().Be("Battle: other");
    }

    // =====================================================================================
    // commands + outbound events
    // =====================================================================================

    [Fact]
    public void AddLabel_CreatesInProvider_AndReturnsTheRow()
    {
        var provider = NewProvider();
        using var vm = NewVm(provider);

        var row = vm.AddLabel(0x7E0000);

        row.SnesAddress.Should().Be(0x7E0000);
        row.Name.Should().Be("New Label");
        provider.GetLabel(0x7E0000).Should().NotBeNull();
        vm.Rows.Should().ContainSingle();
    }

    [Fact]
    public void AddLabel_OnOccupiedAddress_KeepsExistingAndReturnsItsRow()
    {
        var provider = NewProvider((0x010000, "keeper", ""));
        using var vm = NewVm(provider);

        var row = vm.AddLabel(0x010000, "intruder");

        row.Name.Should().Be("keeper");
        vm.TotalLabelCount.Should().Be(1);
    }

    [Fact]
    public void DeleteLabel_RemovesRowAndProviderEntry()
    {
        var provider = NewProvider((0x010000, "goner", ""));
        using var vm = NewVm(provider);

        vm.DeleteLabel(0x010000);

        provider.GetLabel(0x010000).Should().BeNull();
        vm.Rows.Should().BeEmpty();
        vm.TotalLabelCount.Should().Be(0);
    }

    [Fact]
    public void JumpToSelectedInMainView_RaisesNavigationRequested_WithSnesAddress()
    {
        using var vm = NewVm(NewProvider((0x808000, "target", "")));
        var received = new List<int>();
        vm.NavigationRequested += (_, addr) => received.Add(addr);

        vm.JumpToSelectedInMainView();
        received.Should().BeEmpty("nothing selected yet");

        vm.SelectedRow = vm.Rows.Single();
        vm.JumpToSelectedInMainView();
        received.Should().Equal(0x808000);
    }

    [Fact]
    public void FocusOrCreateAtSnesAddress_NormalizesWramMirror_AndClearsSearch()
    {
        var provider = NewProvider((0x808000, "far_away", ""));
        using var vm = NewVm(provider);
        vm.SearchTerm = "far";

        // $001234 is a WRAM mirror of $7E1234; the old editor normalized before creating.
        var row = vm.FocusOrCreateAtSnesAddress(0x001234);

        row.Should().NotBeNull();
        row!.SnesAddress.Should().Be(0x7E1234);
        provider.GetLabel(0x7E1234)!.Name.Should().Be("New Label");
        vm.SearchTerm.Should().BeEmpty("the old editor cleared the filter so the row can't be hidden");
        vm.SelectedRow.Should().BeSameAs(row);
    }

    [Fact]
    public void FocusOrCreateAtSnesAddress_ExistingLabel_SelectsWithoutCreating()
    {
        var provider = NewProvider((0x7E1234, "already_here", ""));
        using var vm = NewVm(provider);

        var row = vm.FocusOrCreateAtSnesAddress(0x7E1234);

        row!.Name.Should().Be("already_here");
        vm.TotalLabelCount.Should().Be(1);
        vm.SelectedRow.Should().BeSameAs(row);
    }

    [Fact]
    public void FocusOrCreateAtRomOffsetIa_UsesInjectedResolver()
    {
        var provider = NewProvider();
        using var vm = NewVm(provider, resolveIa: _ => 0x7E9999);

        var row = vm.FocusOrCreateAtRomOffsetIa(0x1234);

        row!.SnesAddress.Should().Be(0x7E9999);
    }

    [Fact]
    public void FocusOrCreateAtRomOffsetIa_NoIa_RaisesErrorAndReturnsNull()
    {
        using var vm = NewVm(NewProvider(), resolveIa: _ => -1);
        var errors = new List<string>();
        vm.ErrorRaised += (_, msg) => errors.Add(msg);

        vm.FocusOrCreateAtRomOffsetIa(0x1234).Should().BeNull();
        errors.Should().ContainSingle();
    }

    [Fact]
    public void FocusOrCreateAtRomOffsetIa_NoResolverWired_RaisesErrorAndReturnsNull()
    {
        using var vm = NewVm(NewProvider());
        var errors = new List<string>();
        vm.ErrorRaised += (_, msg) => errors.Add(msg);

        vm.FocusOrCreateAtRomOffsetIa(0x1234).Should().BeNull();
        errors.Should().ContainSingle();
    }

    [Fact]
    public void NormalizeWramLabels_InvokesInjectedPort()
    {
        var called = 0;
        using var vm = NewVm(NewProvider(), normalizeWram: () => called++);

        vm.NormalizeWramLabels();

        called.Should().Be(1);
    }

    [Fact]
    public void NormalizeWramLabels_NoPortWired_DefaultsToCoreImplementation()
    {
        // finding 2 RESOLVED: with no port injected, the VM routes to Diz.Core's
        // LabelProviderExtensions.NormalizeWramLabels on its own provider.
        // $001234 is a WRAM mirror; canonical is $7E1234.
        var provider = NewProvider((0x001234, "wram_mirrored", ""));
        using var vm = NewVm(provider);

        vm.NormalizeWramLabels();

        provider.GetLabel(0x001234).Should().BeNull();
        provider.GetLabel(0x7E1234).Should().NotBeNull();
        vm.Rows.Should().ContainSingle().Which.SnesAddress.Should().Be(0x7E1234);
    }

    // =====================================================================================
    // the Thread rule: every notification routes through the injected marshaller
    // =====================================================================================

    [Fact]
    public void Notifications_RouteThroughInjectedMarshaller_NothingLeaksAroundIt()
    {
        var provider = NewProvider((0x010000, "existing", ""));

        // a deferring marshaller: queues everything, applies nothing until drained.
        var queue = new List<Action>();
        using var vm = NewVm(provider, marshaller: queue.Add);
        queue.Clear(); // discard construction-time notifications

        var propertyEvents = new List<string?>();
        var collectionEvents = new List<NotifyCollectionChangedEventArgs>();
        vm.PropertyChanged += (_, e) => propertyEvents.Add(e.PropertyName);
        ((INotifyCollectionChanged)vm.Rows).CollectionChanged += (_, e) => collectionEvents.Add(e);

        provider.AddLabel(0x020000, new Label { Name = "queued" });

        // nothing may have reached us yet: the change is parked in the marshaller.
        vm.Rows.Should().ContainSingle("the row pipeline update itself is marshalled");
        propertyEvents.Should().BeEmpty();
        collectionEvents.Should().BeEmpty();
        queue.Should().NotBeEmpty();

        // drain the marshaller (as a UI thread would); nested marshalled actions join the queue.
        for (var i = 0; i < queue.Count; i++)
            queue[i]();

        vm.Rows.Should().HaveCount(2);
        collectionEvents.Should().ContainSingle().Which.Action.Should().Be(NotifyCollectionChangedAction.Add);
        propertyEvents.Should().Contain(nameof(vm.TotalLabelCount));
    }

    [Fact]
    public void RowPropertyNotifications_AlsoRouteThroughTheMarshaller()
    {
        var provider = NewProvider((0x010000, "before", ""));
        var queue = new List<Action>();
        using var vm = NewVm(provider, marshaller: queue.Add);
        for (var i = 0; i < queue.Count; i++) queue[i](); // settle construction
        var row = vm.Rows.Single();

        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        queue.Clear();

        ((Label)provider.GetLabel(0x010000)!).Name = "after";

        raised.Should().BeEmpty("the row's relay of the model INPC must be marshalled too");
        for (var i = 0; i < queue.Count; i++) queue[i]();
        raised.Should().Contain(nameof(ILabelRowViewModel.Name));
    }

    // =====================================================================================
    // rows / context mappings
    // =====================================================================================

    [Fact]
    public void ContextSummary_FormatsLikeTheOldContextColumn()
    {
        var provider = NewProvider((0x010000, "n", ""));
        var label = (Label)provider.GetLabel(0x010000)!;
        label.ContextMappings.Add(new ContextMapping { Context = "Battle", NameOverride = "hp" });
        label.ContextMappings.Add(new ContextMapping { Context = "  ", NameOverride = "skipped" });
        label.ContextMappings.Add(new ContextMapping { Context = "Menu", NameOverride = "cursor" });

        using var vm = NewVm(provider);

        vm.Rows.Single().ContextSummary.Should().Be("Battle: hp, Menu: cursor");
        vm.Rows.Single().ContextMappings.Should().HaveCount(3);
    }

    [Fact]
    public void ContextMappingViewModel_EditsPassThroughToModel()
    {
        var provider = NewProvider((0x010000, "n", ""));
        var label = (Label)provider.GetLabel(0x010000)!;
        var mapping = new ContextMapping { Context = "Battle", NameOverride = "old" };
        label.ContextMappings.Add(mapping);
        using var vm = NewVm(provider);
        var row = vm.Rows.Single();

        row.ContextMappings.Single().NameOverride = "new";

        mapping.NameOverride.Should().Be("new");
        row.ContextSummary.Should().Be("Battle: new");
    }

    [Fact]
    public void Dispose_UnhooksFromProvider()
    {
        var provider = NewProvider((0x010000, "a", ""));
        var vm = NewVm(provider);
        vm.Dispose();

        // must not throw, and must not resurrect rows:
        provider.AddLabel(0x020000, new Label { Name = "late" });
        vm.Rows.Should().BeEmpty();
    }
}
