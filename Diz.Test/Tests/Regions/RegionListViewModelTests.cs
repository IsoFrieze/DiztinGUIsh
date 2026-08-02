using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using Diz.Ui.ViewModels.Regions;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.Regions;

/// <summary>
/// The region list as state + commands.
///
/// The two things this file exists to pin above all others:
///   1. rows are identified by the REGION OBJECT, never by index, so sorting cannot make a
///      delete hit the wrong region;
///   2. sorting is display-only -- the stored region order, which is what gets serialized and
///      exported, never moves.
/// </summary>
public class RegionListViewModelTests
{
    private static Region NewRegion(
        string name = "region",
        int start = 0x808000,
        int end = 0x80800F) =>
        new()
        {
            RegionName = name,
            StartSnesAddress = start,
            EndSnesAddress = end,
        };

    private static Data NewData(params IRegion[] regions)
    {
        var data = new Data();
        foreach (var region in regions)
            data.Regions.Add(region);
        return data;
    }

    private static List<string> RecordNotifications(RegionListViewModel vm)
    {
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }

    // =========================================================================================
    // rows mirror the region collection
    // =========================================================================================

    [Fact]
    public void RowsAreBuiltForEveryRegionAlreadyInTheProject()
    {
        var data = NewData(NewRegion("a", 0x100, 0x100), NewRegion("b", 0x200, 0x200));
        using var vm = new RegionListViewModel(data);

        vm.Rows.Should().HaveCount(2);
        vm.RegionCount.Should().Be(2);
        vm.Rows.Select(r => r.UnderlyingRegion).Should().Equal(data.Regions);
    }

    [Fact]
    public void ARegionAddedOutsideTheEditor_AppearsAsARow()
    {
        // bank-region synthesis at import and save-format migrations both add regions behind
        // the editor's back; an open list has to notice.
        var data = NewData(NewRegion("a", 0x100, 0x100));
        using var vm = new RegionListViewModel(data);

        data.Regions.Add(NewRegion("b", 0x200, 0x200));

        vm.Rows.Should().HaveCount(2);
        vm.Rows[1].UnderlyingRegion.RegionName.Should().Be("b");
    }

    [Fact]
    public void ARegionRemovedOutsideTheEditor_LosesItsRow()
    {
        var keep = NewRegion("a", 0x100, 0x100);
        var drop = NewRegion("b", 0x200, 0x200);
        var data = NewData(keep, drop);
        using var vm = new RegionListViewModel(data);

        data.Regions.Remove(drop);

        vm.Rows.Should().ContainSingle().Which.UnderlyingRegion.Should().BeSameAs(keep);
    }

    [Fact]
    public void RowInstancesSurviveAnUnrelatedAdd()
    {
        var data = NewData(NewRegion("a", 0x100, 0x100));
        using var vm = new RegionListViewModel(data);
        var original = vm.Rows[0];

        data.Regions.Add(NewRegion("b", 0x200, 0x200));

        vm.Rows.Should().Contain(original);
    }

    // =========================================================================================
    // adding: a new row is usable immediately, and is never a trap
    // =========================================================================================

    [Fact]
    public void AddRegion_SeedsANameSoTheRowIsValidImmediately()
    {
        var data = NewData();
        using var vm = new RegionListViewModel(data);

        var row = vm.AddRegion();

        row.UnderlyingRegion.RegionName.Should().Be("New Region");
        row.HasError.Should().BeFalse();
    }

    [Fact]
    public void AddRegion_SeedsALegalOneByteRange()
    {
        var data = NewData();
        using var vm = new RegionListViewModel(data);

        var row = vm.AddRegion();

        row.UnderlyingRegion.StartSnesAddress.Should().Be(row.UnderlyingRegion.EndSnesAddress);
        ((RegionRowViewModel)row).RegionLength.Should().Be(1);
        row.LengthText.Should().Be("1");
    }

    [Fact]
    public void AddRegion_AddsExactlyOneRegionToTheProject()
    {
        var data = NewData(NewRegion("existing"));
        using var vm = new RegionListViewModel(data);

        var row = vm.AddRegion();

        data.Regions.Should().HaveCount(2);
        data.Regions.Should().Contain(row.UnderlyingRegion);
    }

    [Fact]
    public void AddRegion_ReturnsARowThatIsInTheRowSet()
    {
        var data = NewData();
        using var vm = new RegionListViewModel(data);

        var row = vm.AddRegion();

        vm.Rows.Should().ContainSingle().Which.Should().BeSameAs(row);
    }

    // =========================================================================================
    // deleting: BY REGION OBJECT. this is the whole point of row identity.
    // =========================================================================================

    [Fact]
    public void DeleteRegion_RemovesThatExactRegion()
    {
        var a = NewRegion("a", 0x100, 0x100);
        var b = NewRegion("b", 0x200, 0x200);
        var data = NewData(a, b);
        using var vm = new RegionListViewModel(data);

        vm.DeleteRegion(vm.Rows.Single(r => ReferenceEquals(r.UnderlyingRegion, b)));

        data.Regions.Should().ContainSingle().Which.Should().BeSameAs(a);
    }

    [Fact]
    public void DeleteRegion_WhileSortedDescending_RemovesTheRegionTheRowActuallyPointsAt()
    {
        // THE regression test for deleting by grid index. Sorted descending, display row 1 is
        // the MIDDLE region by address but sits at a different index in the stored collection.
        var a = NewRegion("a", 0x100, 0x100);
        var b = NewRegion("b", 0x200, 0x200);
        var c = NewRegion("c", 0x300, 0x300);
        var data = NewData(a, b, c);
        using var vm = new RegionListViewModel(data) { SortField = RegionField.Start, SortDescending = true };

        vm.Rows.Select(r => r.UnderlyingRegion).Should().Equal(c, b, a);

        vm.DeleteRegion(vm.Rows[1]);

        data.Regions.Should().Equal(a, c);
        data.Regions.Should().NotContain(b);
    }

    [Fact]
    public void DeleteRegion_ClearsTheSelectionWhenTheSelectedRegionGoes()
    {
        var data = NewData(NewRegion("a", 0x100, 0x100), NewRegion("b", 0x200, 0x200));
        using var vm = new RegionListViewModel(data);
        vm.SelectedRow = vm.Rows[1];

        vm.DeleteRegion(vm.Rows[1]);

        vm.SelectedRow.Should().BeNull();
    }

    [Fact]
    public void DeleteRegion_LeavesTheSelectionAloneWhenAnotherRegionGoes()
    {
        var data = NewData(NewRegion("a", 0x100, 0x100), NewRegion("b", 0x200, 0x200));
        using var vm = new RegionListViewModel(data);
        var keep = vm.Rows[0];
        vm.SelectedRow = keep;

        vm.DeleteRegion(vm.Rows[1]);

        vm.SelectedRow.Should().BeSameAs(keep);
    }

    [Fact]
    public void DeleteRegion_IsANoOpForARegionAlreadyGone()
    {
        var region = NewRegion("a", 0x100, 0x100);
        var data = NewData(region);
        using var vm = new RegionListViewModel(data);
        var row = vm.Rows[0];
        data.Regions.Remove(region);

        var act = () => vm.DeleteRegion(row);

        act.Should().NotThrow();
        data.Regions.Should().BeEmpty();
    }

    // =========================================================================================
    // sorting: display only
    // =========================================================================================

    private static (RegionListViewModel vm, Region first, Region second, Data data)
        TwoRegionsDifferingInEveryField()
    {
        var first = new Region
        {
            StartSnesAddress = 0x100, EndSnesAddress = 0x100,
            RegionName = "aaa", ContextToApply = "aaa", Priority = 1,
            ExportSeparateFile = false, ExportType = RegionExportType.Assembly,
            AssetType = "aaa", AssetVersion = "aaa", AssetName = "aaa", AssetOptions = "aaa",
        };
        var second = new Region
        {
            StartSnesAddress = 0x200, EndSnesAddress = 0x300,
            RegionName = "bbb", ContextToApply = "bbb", Priority = 2,
            ExportSeparateFile = true, ExportType = RegionExportType.Binary,
            AssetType = "bbb", AssetVersion = "bbb", AssetName = "bbb", AssetOptions = "bbb",
        };

        var data = NewData(first, second);
        return (new RegionListViewModel(data), first, second, data);
    }

    [Theory]
    [InlineData(RegionField.Start)]
    [InlineData(RegionField.End)]
    [InlineData(RegionField.Length)]
    [InlineData(RegionField.RegionName)]
    [InlineData(RegionField.ContextToApply)]
    [InlineData(RegionField.Priority)]
    [InlineData(RegionField.ExportSeparateFile)]
    [InlineData(RegionField.ExportType)]
    [InlineData(RegionField.AssetType)]
    [InlineData(RegionField.AssetVersion)]
    [InlineData(RegionField.AssetName)]
    [InlineData(RegionField.AssetOptions)]
    public void EveryFieldSortsBothWays(RegionField field)
    {
        var (vm, first, second, _) = TwoRegionsDifferingInEveryField();
        using (vm)
        {
            vm.SortField = field;

            vm.SortDescending = false;
            vm.Rows.Select(r => r.UnderlyingRegion).Should().Equal(first, second);

            vm.SortDescending = true;
            vm.Rows.Select(r => r.UnderlyingRegion).Should().Equal(second, first);
        }
    }

    [Fact]
    public void RowsComeOutOfTheViewModelAlreadySorted()
    {
        // the view never sorts: it renders Rows in order.
        var data = NewData(NewRegion("c", 0x300, 0x300), NewRegion("a", 0x100, 0x100), NewRegion("b", 0x200, 0x200));
        using var vm = new RegionListViewModel(data);

        vm.Rows.Select(r => r.UnderlyingRegion.RegionName).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void SortingDoesNotReorderTheStoredRegionCollection()
    {
        // the stored order is what gets serialized and exported. It must not move.
        var data = NewData(NewRegion("c", 0x300, 0x300), NewRegion("a", 0x100, 0x100), NewRegion("b", 0x200, 0x200));
        var storedOrder = data.Regions.ToList();
        using var vm = new RegionListViewModel(data);

        vm.SortField = RegionField.RegionName;
        vm.SortDescending = true;
        vm.SortField = RegionField.Priority;
        vm.SortDescending = false;

        data.Regions.Should().Equal(storedOrder);
    }

    [Fact]
    public void TiesKeepTheirRelativeOrderInBothDirections()
    {
        var a = NewRegion("same", 0x100, 0x100);
        var b = NewRegion("same", 0x200, 0x200);
        var data = NewData(a, b);
        using var vm = new RegionListViewModel(data) { SortField = RegionField.RegionName };

        vm.Rows.Select(r => r.UnderlyingRegion).Should().Equal(a, b);

        vm.SortDescending = true;

        vm.Rows.Select(r => r.UnderlyingRegion).Should().Equal(a, b);
    }

    [Fact]
    public void ChangingASortedValue_MovesTheRow()
    {
        var a = NewRegion("a", 0x100, 0x400);
        var b = NewRegion("b", 0x200, 0x200);
        var data = NewData(a, b);
        using var vm = new RegionListViewModel(data);

        vm.CommitField(vm.Rows.Single(r => ReferenceEquals(r.UnderlyingRegion, a)), RegionField.Start, "300");

        vm.Rows.Select(r => r.UnderlyingRegion).Should().Equal(b, a);
    }

    [Fact]
    public void SortStateRaisesItsOwnNotifications()
    {
        var data = NewData(NewRegion());
        using var vm = new RegionListViewModel(data);
        var raised = RecordNotifications(vm);

        vm.SortField = RegionField.RegionName;
        vm.SortDescending = true;

        raised.Should().Contain(nameof(vm.SortField));
        raised.Should().Contain(nameof(vm.SortDescending));
    }

    // =========================================================================================
    // selection is ViewModel state
    // =========================================================================================

    [Fact]
    public void SelectedRow_SurvivesAReSort()
    {
        var data = NewData(NewRegion("a", 0x100, 0x100), NewRegion("b", 0x200, 0x200));
        using var vm = new RegionListViewModel(data);
        var selected = vm.Rows[0];
        vm.SelectedRow = selected;

        vm.SortDescending = true;

        vm.SelectedRow.Should().BeSameAs(selected);
        vm.Rows.Last().Should().BeSameAs(selected);
    }

    [Fact]
    public void SelectedRow_RaisesExactlyOneNotification()
    {
        var data = NewData(NewRegion());
        using var vm = new RegionListViewModel(data);
        var raised = RecordNotifications(vm);

        vm.SelectedRow = vm.Rows[0];

        raised.Should().ContainSingle().Which.Should().Be(nameof(vm.SelectedRow));
    }

    [Fact]
    public void SelectedRow_SetToTheSameRowTwice_NotifiesOnce()
    {
        var data = NewData(NewRegion());
        using var vm = new RegionListViewModel(data);
        vm.SelectedRow = vm.Rows[0];
        var raised = RecordNotifications(vm);

        vm.SelectedRow = vm.Rows[0];

        raised.Should().BeEmpty();
    }

    // =========================================================================================
    // length <-> end address arithmetic, inclusive
    // =========================================================================================

    [Fact]
    public void LengthOfOne_PutsTheEndAddressOnTheStartAddress()
    {
        // the newly legal one-byte region, reachable by typing a length of 1.
        var region = NewRegion(start: 0x808000, end: 0x80800F);
        var data = NewData(region);
        using var vm = new RegionListViewModel(data);

        vm.CommitField(vm.Rows[0], RegionField.Length, "1").IsValid.Should().BeTrue();

        region.EndSnesAddress.Should().Be(0x808000);
        region.StartSnesAddress.Should().Be(0x808000);
    }

    [Fact]
    public void LengthOf0x10_PutsTheEndAddressFifteenBytesPastTheStart()
    {
        var region = NewRegion(start: 0x808000, end: 0x808000);
        var data = NewData(region);
        using var vm = new RegionListViewModel(data);

        vm.CommitField(vm.Rows[0], RegionField.Length, "10").IsValid.Should().BeTrue();

        region.EndSnesAddress.Should().Be(0x80800F);
    }

    [Fact]
    public void EditingTheLength_LeavesTheStartAddressWhereItIs()
    {
        var region = NewRegion(start: 0x808000, end: 0x80800F);
        var data = NewData(region);
        using var vm = new RegionListViewModel(data);

        vm.CommitField(vm.Rows[0], RegionField.Length, "100");

        region.StartSnesAddress.Should().Be(0x808000);
        region.EndSnesAddress.Should().Be(0x8080FF);
    }

    [Fact]
    public void EditingEitherAddress_RecomputesTheLength()
    {
        var region = NewRegion(start: 0x808000, end: 0x80800F);
        var data = NewData(region);
        using var vm = new RegionListViewModel(data);
        var row = vm.Rows[0];

        vm.CommitField(row, RegionField.End, "80801F");
        row.LengthText.Should().Be("20");

        vm.CommitField(row, RegionField.Start, "808010");
        row.LengthText.Should().Be("10");
    }

    [Fact]
    public void LengthBelowOne_IsRefused()
    {
        var region = NewRegion(start: 0x808000, end: 0x80800F);
        var data = NewData(region);
        using var vm = new RegionListViewModel(data);

        var result = vm.CommitField(vm.Rows[0], RegionField.Length, "0");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Length must be at least 1 (zero-length regions are not allowed).");
        region.EndSnesAddress.Should().Be(0x80800F);
    }

    [Fact]
    public void LengthThatIsNotANumber_IsRefused()
    {
        var data = NewData(NewRegion());
        using var vm = new RegionListViewModel(data);

        var result = vm.CommitField(vm.Rows[0], RegionField.Length, "!!!");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Invalid length: '!!!'. Please enter a valid hexadecimal number.");
    }

    // =========================================================================================
    // the row gauntlet, driven through the editing surface. one named test per rule.
    // =========================================================================================

    private static (RegionListViewModel vm, IRegionRowViewModel row, Region region) OneRow(
        Region region = null)
    {
        var model = region ?? NewRegion();
        var data = NewData(model);
        var vm = new RegionListViewModel(data);
        return (vm, vm.Rows[0], model);
    }

    [Fact]
    public void Check1_ABlankRegionName_IsRefused()
    {
        var (vm, row, region) = OneRow();

        var result = vm.CommitField(row, RegionField.RegionName, "   ");

        result.Error.Should().Be("Region Name is required.");
        region.RegionName.Should().Be("region");
    }

    [Fact]
    public void Check2_AStartAddressThatIsNotANumber_IsRefused()
    {
        var (vm, row, region) = OneRow();

        var result = vm.CommitField(row, RegionField.Start, "!!!");

        result.Error.Should().Be("Start SNES address must be valid number");
        region.StartSnesAddress.Should().Be(0x808000);
    }

    [Fact]
    public void Check3_AnEndAddressThatIsNotANumber_IsRefused()
    {
        var (vm, row, region) = OneRow();

        var result = vm.CommitField(row, RegionField.End, "!!!");

        result.Error.Should().Be("End SNES address must be valid number");
        region.EndSnesAddress.Should().Be(0x80800F);
    }

    [Fact]
    public void Check4_AStartEqualToTheEnd_IsNowAccepted_AsAOneByteRegion()
    {
        // the deliberate behavior change. The old grid refused this as "zero-length" while its
        // own length column produced it.
        var (vm, row, region) = OneRow();

        var result = vm.CommitField(row, RegionField.End, "808000");

        result.IsValid.Should().BeTrue();
        region.EndSnesAddress.Should().Be(region.StartSnesAddress);
        row.LengthText.Should().Be("1");
    }

    [Fact]
    public void Check5_AStartGreaterThanTheEnd_IsStillRefused()
    {
        // regression guard for check 4: equality became legal, inversion did not.
        var (vm, row, region) = OneRow();

        var result = vm.CommitField(row, RegionField.Start, "808010");

        result.Error.Should().Be("Start address must not be greater than end address.");
        region.StartSnesAddress.Should().Be(0x808000);
    }

    [Fact]
    public void Check6_ANegativeAddress_IsRefused()
    {
        // pasting a 32-bit value reads back as negative -- the historical way to trip this.
        var (vm, row, region) = OneRow();

        var result = vm.CommitField(row, RegionField.Start, "FFFFFFFF");

        result.Error.Should().Be("Negative numbers not allowed in SNES addresses");
        region.StartSnesAddress.Should().Be(0x808000);
    }

    [Fact]
    public void Check7_AnAddressAbove24Bits_IsRefused()
    {
        var (vm, row, region) = OneRow();

        var result = vm.CommitField(row, RegionField.End, "1000000");

        result.Error.Should().Be("SNES address too large (max allowed: 24-bits: 0xFFFFFF)");
        region.EndSnesAddress.Should().Be(0x80800F);
    }

    /// <summary>
    /// Regression guard: a region that emits its own file may span bank boundaries, and there is
    /// no row rule about banks at all. The only constraint on file-producing regions is between
    /// regions -- they must nest, never partially overlap -- and banks play no part in it.
    /// (There used to be a row check refusing exactly this edit; the emitted assembly handles a
    /// bank seam inside a file on purpose, so the check was refusing valid data.)
    /// </summary>
    [Fact]
    public void Check8_ASeparateFileRegionCrossingABank_IsAccepted()
    {
        var (vm, row, region) = OneRow(NewRegion("r", 0x80FFF0, 0x810010));

        var result = vm.CommitField(row, RegionField.ExportSeparateFile, "True");

        result.IsValid.Should().BeTrue();
        region.ExportSeparateFile.Should().BeTrue();
        row.HasError.Should().BeFalse();
    }

    [Fact]
    public void Check9_AnAssetNameThatEscapesTheAssetRoot_IsRefused()
    {
        var (vm, row, region) = OneRow();

        var result = vm.CommitField(row, RegionField.AssetName, "../elsewhere");

        result.Error.Should().Be(
            "Asset Name must be a relative path: no backslashes, no '..', and no leading '/'.");
        region.AssetName.Should().BeNull();
    }

    [Fact]
    public void Check10_TheAssetRules_ApplyOnceTheExportTypeIsAsset()
    {
        // 0x10 bytes is not a whole number of 4bpp tiles (0x20 each).
        var region = NewRegion("r", 0x808000, 0x80800F);
        region.AssetType = "gfx.snes.4bpp";
        var (vm, row, _) = OneRow(region);

        var result = vm.CommitField(row, RegionField.ExportType, "Asset");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("must be a whole multiple of");
        region.ExportType.Should().Be(RegionExportType.Assembly);
    }

    [Fact]
    public void AnUnparseableExportType_IsRefused()
    {
        var (vm, row, region) = OneRow();

        var result = vm.CommitField(row, RegionField.ExportType, "Nonsense");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Export Type must be one of: Assembly, Binary, Asset.");
        region.ExportType.Should().Be(RegionExportType.Assembly);
    }

    [Fact]
    public void AnUnparseablePriority_IsRefused()
    {
        var (vm, row, region) = OneRow();

        var result = vm.CommitField(row, RegionField.Priority, "high");

        result.IsValid.Should().BeFalse();
        region.Priority.Should().Be(0);
    }

    [Fact]
    public void ValidateField_NeverMutatesAnything()
    {
        var (vm, row, region) = OneRow();
        var written = new List<string>();
        region.PropertyChanged += (_, e) => written.Add(e.PropertyName);

        vm.ValidateField(row, RegionField.Start, "808004").IsValid.Should().BeTrue();
        vm.ValidateField(row, RegionField.Start, "!!!").IsValid.Should().BeFalse();

        written.Should().BeEmpty();
        region.StartSnesAddress.Should().Be(0x808000);
        row.HasError.Should().BeFalse("validating is not committing");
    }

    // =========================================================================================
    // an invalid edit never reaches the model
    // =========================================================================================

    [Fact]
    public void ARefusedEdit_LeavesEverySingleRegionFieldUntouched()
    {
        var region = new Region
        {
            RegionName = "keep", StartSnesAddress = 0x808000, EndSnesAddress = 0x80800F,
            ContextToApply = "Battle", Priority = 3, ExportSeparateFile = false,
            ExportType = RegionExportType.Assembly, AssetType = "gfx.snes.4bpp",
            AssetVersion = "v1", AssetName = "gfx/font", AssetOptions = "{}",
        };
        var (vm, row, _) = OneRow(region);
        var before = Snapshot(region);

        vm.CommitField(row, RegionField.Start, "FFFFFF").IsValid.Should().BeFalse();

        Snapshot(region).Should().Be(before);
    }

    [Fact]
    public void ARefusedEdit_PutsItsMessageInTheStatusLine()
    {
        var (vm, row, _) = OneRow();

        vm.CommitField(row, RegionField.RegionName, "");

        vm.StatusText.Should().Be("Region Name is required.");
        row.HasError.Should().BeTrue();
    }

    [Fact]
    public void ASuccessfulEdit_ClearsTheStatusLine()
    {
        var (vm, row, _) = OneRow();
        vm.CommitField(row, RegionField.RegionName, "");

        vm.CommitField(row, RegionField.RegionName, "fixed");

        vm.StatusText.Should().Be("");
        row.HasError.Should().BeFalse();
    }

    [Fact]
    public void AnEditIsRefusedWhileTheRestOfTheRowIsInvalid_AndSucceedsOnceItIsFixed()
    {
        // the rules are row-scoped, exactly as the grid's were -- minus the trapped focus.
        var (vm, row, region) = OneRow(NewRegion("r", 0x808010, 0x808000));

        vm.CommitField(row, RegionField.ContextToApply, "Battle").IsValid.Should().BeFalse();
        region.ContextToApply.Should().BeNull();

        vm.CommitField(row, RegionField.End, "808020").IsValid.Should().BeTrue();
        vm.CommitField(row, RegionField.ContextToApply, "Battle").IsValid.Should().BeTrue();
        region.ContextToApply.Should().Be("Battle");
    }

    [Fact]
    public void ARowStillShowingRefusedText_StaysFlaggedAfterASuccessfulEditElsewhereOnTheRow()
    {
        // the marker answers "is what I am looking at in the model?", not "are the stored values
        // legal?". Fixing the name does not put the refused address into the region, so the row
        // is still lying to the user until that text is dealt with.
        var (vm, row, region) = OneRow();
        vm.CommitField(row, RegionField.Start, "!!!").IsValid.Should().BeFalse();

        vm.CommitField(row, RegionField.RegionName, "renamed").IsValid.Should().BeTrue();

        row.HasError.Should().BeTrue("the row is still displaying an address the model refused");
        row.ErrorText.Should().Be("Start SNES address must be valid number");
        row.StartText.Should().Be("!!!");
        region.StartSnesAddress.Should().Be(0x808000);
        region.RegionName.Should().Be("renamed");
    }

    [Fact]
    public void CorrectingTheRefusedField_FinallyClearsTheFlag()
    {
        var (vm, row, _) = OneRow();
        vm.CommitField(row, RegionField.Start, "!!!");
        vm.CommitField(row, RegionField.RegionName, "renamed");

        vm.CommitField(row, RegionField.Start, "808004").IsValid.Should().BeTrue();

        row.HasError.Should().BeFalse();
        row.ErrorText.Should().Be("");
    }

    [Fact]
    public void ARefusedClosedValueEdit_LeavesNoStaleMarker_BecauseNothingRefusedIsOnScreen()
    {
        // A bool and an enum are carried by widgets whose value space is closed -- a checkbox, a
        // combo -- so a refused edit to one of them leaves NOTHING on screen that the model did
        // not accept: the widget shows the committed value again. Flagging the row would mark it
        // forever over a value that is neither displayed nor stored.
        var region = NewRegion("r", 0x808000, 0x80800F);
        region.AssetType = "gfx.snes.4bpp"; // 0x10 bytes is not a whole number of 4bpp tiles
        var (vm, row, _) = OneRow(region);

        vm.CommitField(row, RegionField.ExportType, "Asset").IsValid.Should().BeFalse();

        row.HasError.Should().BeFalse();
        row.ErrorText.Should().Be("");
        row.HasPendingTextFor(RegionField.ExportType).Should().BeFalse();
        row.ExportTypeText.Should().Be("Assembly", "the combo snapped back to what the model holds");
        // the refusal is still reported -- on the status line, which is where a message that has
        // nowhere to live on the row belongs.
        vm.StatusText.Should().Contain("must be a whole multiple of");
    }

    [Fact]
    public void ARefusedClosedValueEdit_LetsTheModelsOwnVerdictKeepTheMarker()
    {
        // clearing the refusal record must not clear a marker the STORED values earned: those two
        // are separate, and the row is still wrong for its own reasons.
        // 10 bytes is not a whole number of 9-byte BRR blocks, so switching to Asset is refused.
        var region = NewRegion("r", 0x808000, 0x808009);
        region.AssetType = "audio.snes.brr";
        var (vm, row, _) = OneRow(region);

        // an illegal export type is refused, and the combo snaps back...
        vm.CommitField(row, RegionField.ExportType, "Asset").IsValid.Should().BeFalse();
        row.HasError.Should().BeFalse();

        // ... but a row whose stored values break a rule stays flagged, refusal or no refusal.
        vm.CommitField(row, RegionField.RegionName, "").IsValid.Should().BeFalse();
        row.HasError.Should().BeTrue();
        row.ErrorText.Should().Be("Region Name is required.");
    }

    [Fact]
    public void RetryingARefusedClosedValueEdit_IsAFreshAttempt_NotANoOp()
    {
        // the retry path: refused once, the reason is fixed, the same value is offered again and
        // must now go in. Nothing may be left over from the first attempt that makes the second
        // one look like it changed nothing.
        // 10 bytes is not a whole number of 9-byte BRR blocks; 9 bytes is exactly one.
        var seed = NewRegion("r", 0x808000, 0x808009);
        seed.AssetType = "audio.snes.brr";
        var (vm, row, region) = OneRow(seed);

        vm.CommitField(row, RegionField.ExportType, "Asset").IsValid.Should().BeFalse();
        region.ExportType.Should().Be(RegionExportType.Assembly);

        vm.CommitField(row, RegionField.End, "808008").IsValid.Should().BeTrue();
        vm.CommitField(row, RegionField.ExportType, "Asset").IsValid.Should().BeTrue();

        region.ExportType.Should().Be(RegionExportType.Asset);
        row.HasError.Should().BeFalse();
    }

    [Fact]
    public void AnIgnoredBlankEdit_DoesNotFlagTheRow()
    {
        // pending text and refused text are not the same thing: an empty box is neither.
        var (vm, row, _) = OneRow();

        vm.CommitField(row, RegionField.Start, "   ");

        row.HasPendingTextFor(RegionField.Start).Should().BeTrue();
        row.HasError.Should().BeFalse();
    }

    // =========================================================================================
    // a row belongs to exactly one list
    // =========================================================================================

    [Fact]
    public void CommittingARowFromAnotherRegionList_ThrowsAndChangesNothing()
    {
        // same type, valid region, different project. A type check alone would let this through
        // and quietly edit the other project's data.
        var foreignRegion = NewRegion("foreign");
        var foreignData = NewData(foreignRegion);
        using var foreignVm = new RegionListViewModel(foreignData);
        var foreignRow = foreignVm.Rows[0];

        var data = NewData(NewRegion("ours"));
        using var vm = new RegionListViewModel(data);
        var before = Snapshot(foreignRegion);

        var act = () => vm.CommitField(foreignRow, RegionField.RegionName, "hijacked");

        act.Should().Throw<ArgumentException>();
        Snapshot(foreignRegion).Should().Be(before);
        foreignRegion.RegionName.Should().Be("foreign");
    }

    [Fact]
    public void ValidatingARowFromAnotherRegionList_Throws()
    {
        var foreignData = NewData(NewRegion("foreign"));
        using var foreignVm = new RegionListViewModel(foreignData);
        var foreignRow = foreignVm.Rows[0];

        var data = NewData(NewRegion("ours"));
        using var vm = new RegionListViewModel(data);

        var act = () => vm.ValidateField(foreignRow, RegionField.RegionName, "anything");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DeletingWithARowFromAnotherRegionList_TouchesNeitherList()
    {
        // delete is safe by construction -- it only ever looks for the region in ITS OWN
        // collection -- so a foreign row matches nothing rather than throwing.
        var foreignData = NewData(NewRegion("foreign"));
        using var foreignVm = new RegionListViewModel(foreignData);

        var data = NewData(NewRegion("ours"));
        using var vm = new RegionListViewModel(data);

        vm.DeleteRegion(foreignVm.Rows[0]);

        data.Regions.Should().HaveCount(1);
        foreignData.Regions.Should().HaveCount(1);
    }

    private static string Snapshot(IRegion r) =>
        string.Join("|", r.StartSnesAddress, r.EndSnesAddress, r.RegionName, r.ContextToApply,
            r.Priority, r.ExportSeparateFile, r.ExportType, r.AssetType, r.AssetVersion,
            r.AssetName, r.AssetOptions);

    // =========================================================================================
    // blank means "no input", not zero; numbers mean the same on every machine
    // =========================================================================================

    [Theory]
    [InlineData(RegionField.Start)]
    [InlineData(RegionField.End)]
    [InlineData(RegionField.Length)]
    [InlineData(RegionField.Priority)]
    public void ABlankNumericField_LeavesTheModelWhereItIs(RegionField field)
    {
        var (vm, row, region) = OneRow();
        var before = Snapshot(region);

        vm.CommitField(row, field, "   ").IsValid.Should().BeTrue();

        Snapshot(region).Should().Be(before);
        row.HasError.Should().BeFalse("an empty box is not a mistake");
    }

    [Fact]
    public void ABlankNumericField_KeepsWhatTheUserTyped()
    {
        var (vm, row, _) = OneRow();

        vm.CommitField(row, RegionField.Start, "");

        row.HasPendingTextFor(RegionField.Start).Should().BeTrue();
        row.StartText.Should().Be("");
    }

    [Theory]
    [InlineData("808004")]
    [InlineData("$808004")]
    [InlineData("80/8004")]
    [InlineData("CODE_808004")]
    [InlineData("80-80-04")]
    public void AddressesAcceptWhateverTheDisassemblyPrints(string typed)
    {
        var (vm, row, region) = OneRow();

        vm.CommitField(row, RegionField.Start, typed).IsValid.Should().BeTrue();

        region.StartSnesAddress.Should().Be(0x808004);
    }

    [Fact]
    public void NumbersAreReadTheSameWayOnEveryMachine()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // a culture whose negative sign is not '-': ambient-culture parsing would refuse
            // "-5", invariant parsing accepts it.
            var hostile = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            hostile.NumberFormat.NegativeSign = "!";
            CultureInfo.CurrentCulture = hostile;

            var (vm, row, region) = OneRow();

            vm.CommitField(row, RegionField.Start, "808004").IsValid.Should().BeTrue();
            vm.CommitField(row, RegionField.Priority, "-5").IsValid.Should().BeTrue();

            region.StartSnesAddress.Should().Be(0x808004);
            region.Priority.Should().Be(-5);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // =========================================================================================
    // only the field being edited is written
    // =========================================================================================

    [Theory]
    [InlineData(RegionField.Start, "808004", nameof(IRegion.StartSnesAddress))]
    [InlineData(RegionField.End, "808020", nameof(IRegion.EndSnesAddress))]
    [InlineData(RegionField.Length, "40", nameof(IRegion.EndSnesAddress))]
    [InlineData(RegionField.RegionName, "renamed", nameof(IRegion.RegionName))]
    [InlineData(RegionField.ContextToApply, "Battle", nameof(IRegion.ContextToApply))]
    [InlineData(RegionField.Priority, "5", nameof(IRegion.Priority))]
    [InlineData(RegionField.ExportSeparateFile, "True", nameof(IRegion.ExportSeparateFile))]
    [InlineData(RegionField.ExportType, "Binary", nameof(IRegion.ExportType))]
    [InlineData(RegionField.AssetType, "gfx.snes.4bpp", nameof(IRegion.AssetType))]
    [InlineData(RegionField.AssetVersion, "v2", nameof(IRegion.AssetVersion))]
    [InlineData(RegionField.AssetName, "gfx/font", nameof(IRegion.AssetName))]
    [InlineData(RegionField.AssetOptions, "{}", nameof(IRegion.AssetOptions))]
    public void ACommitWritesExactlyOneRegionProperty(RegionField field, string text, string expected)
    {
        var (vm, row, region) = OneRow();
        var written = new List<string>();
        region.PropertyChanged += (_, e) => written.Add(e.PropertyName);

        vm.CommitField(row, field, text).IsValid.Should().BeTrue();

        written.Should().ContainSingle().Which.Should().Be(expected);
    }

    [Fact]
    public void BuildingTheViewModel_WritesNothingToAnyRegion()
    {
        var region = NewRegion();
        var written = new List<string>();
        region.PropertyChanged += (_, e) => written.Add(e.PropertyName);
        var data = NewData(region);

        using var vm = new RegionListViewModel(data);
        vm.SortField = RegionField.RegionName;
        vm.SortDescending = true;
        vm.RevalidateAll();

        written.Should().BeEmpty();
    }

    // =========================================================================================
    // problems that only exist between regions
    // =========================================================================================

    [Fact]
    public void OverlappingAssetRegions_AreReported()
    {
        // 0x100 bytes is a whole number of 2bpp tiles, so neither row has a problem of its own:
        // the ONLY thing wrong here is the relationship between the two.
        var a = AssetRegion("a", 0x100, 0x1FF);
        var b = AssetRegion("b", 0x180, 0x27F);
        var data = NewData(a, b);
        using var vm = new RegionListViewModel(data);

        ErrorMessages(vm).Should().BeEquivalentTo(RegionValidation.ValidateNonCrossing(data.Regions));
        ErrorMessages(vm).Should().NotBeEmpty();
    }

    [Fact]
    public void PartiallyCrossingFileProducingRegions_AreReported()
    {
        var a = NewRegion("a", 0x100, 0x200);
        var b = NewRegion("b", 0x180, 0x280);
        a.ExportSeparateFile = true;
        b.ExportSeparateFile = true;
        var data = NewData(a, b);
        using var vm = new RegionListViewModel(data);

        ErrorMessages(vm).Should().BeEquivalentTo(RegionValidation.ValidateNonCrossing(data.Regions));
        ErrorMessages(vm).Should().NotBeEmpty();
    }

    [Fact]
    public void ARegionClaimingBothOutputRoles_IsReported()
    {
        var a = AssetRegion("a", 0x100, 0x1FF);
        a.ExportSeparateFile = true;
        var data = NewData(a);
        using var vm = new RegionListViewModel(data);

        ErrorMessages(vm).Should().BeEquivalentTo(RegionValidation.ValidateNonCrossing(data.Regions));
        ErrorMessages(vm).Should().NotBeEmpty();
    }

    [Fact]
    public void DuplicateRegionNames_AreReportedAsAWarningNotAnError()
    {
        // existing projects may already contain duplicates; they still have to load and edit.
        var data = NewData(NewRegion("same", 0x100, 0x100), NewRegion("SAME", 0x200, 0x200));
        using var vm = new RegionListViewModel(data);

        var warnings = vm.Problems.Where(p => p.Severity == RegionProblemSeverity.Warning).ToList();

        warnings.Should().ContainSingle();
        warnings[0].Message.Should().Contain("'same'").And.Contain("2 regions");
        ErrorMessages(vm).Should().BeEmpty();
    }

    [Fact]
    public void ACleanListHasNoProblems()
    {
        var data = NewData(NewRegion("a", 0x100, 0x100), NewRegion("b", 0x200, 0x200));
        using var vm = new RegionListViewModel(data);

        vm.Problems.Should().BeEmpty();
    }

    [Fact]
    public void ProblemsClearOnceTheDataIsFixed()
    {
        // Binary counts as an asset region for the overlap rule, and unlike Asset it carries no
        // per-row asset requirements -- so the row stays editable while the overlap is fixed.
        var a = NewRegion("a", 0x100, 0x200);
        var b = NewRegion("b", 0x180, 0x280);
        a.ExportType = RegionExportType.Binary;
        b.ExportType = RegionExportType.Binary;
        var data = NewData(a, b);
        using var vm = new RegionListViewModel(data);
        vm.Problems.Should().NotBeEmpty();

        vm.CommitField(vm.Rows.Single(r => ReferenceEquals(r.UnderlyingRegion, b)), RegionField.Start, "210");

        vm.Problems.Should().BeEmpty();
    }

    [Fact]
    public void ProblemsAppearWhenAnExternalChangeCreatesThem()
    {
        var a = AssetRegion("a", 0x100, 0x1FF);
        var data = NewData(a);
        using var vm = new RegionListViewModel(data);
        vm.Problems.Should().BeEmpty();

        data.Regions.Add(AssetRegion("b", 0x180, 0x27F));

        ErrorMessages(vm).Should().NotBeEmpty();
    }

    // =========================================================================================
    // problems a single row already has, which the report used to leave out entirely
    // =========================================================================================

    [Fact]
    public void ARowWhoseStoredValuesBreakARule_IsReported()
    {
        // a project on disk can hold anything; nothing stops a region arriving unnamed. The grid
        // flags such a row -- so the report has to list it, or the two disagree on screen.
        var unnamed = NewRegion("", 0x808000, 0x80800F);
        var data = NewData(unnamed);
        using var vm = new RegionListViewModel(data);

        vm.Rows[0].HasError.Should().BeTrue();
        vm.Problems.Should().ContainSingle();
        vm.Problems[0].Severity.Should().Be(RegionProblemSeverity.Error);
        vm.Problems[0].Message.Should().Be(
            "(unnamed region) ($808000-$80800F): Region Name is required.");
        vm.Problems[0].Region.Should().BeSameAs(unnamed);
    }

    [Fact]
    public void ARowProblemNamesTheRegionAndItsRange_BecauseTheRuleMessageAloneCouldBeAnyRow()
    {
        // 0x11 bytes is not a whole number of 2bpp tiles.
        var bad = NewRegion("tiles", 0x808000, 0x808010);
        bad.ExportType = RegionExportType.Asset;
        bad.AssetType = "gfx.snes.2bpp";
        using var vm = new RegionListViewModel(NewData(bad));

        vm.Problems.Should().ContainSingle();
        vm.Problems[0].Message.Should().StartWith("tiles ($808000-$808010): ")
            .And.Contain("must be a whole multiple of");
        vm.Problems[0].Region.Should().BeSameAs(bad);
    }

    [Fact]
    public void FixingTheStoredValues_TakesTheRowProblemOutOfTheReport()
    {
        var data = NewData(NewRegion("", 0x808000, 0x80800F));
        using var vm = new RegionListViewModel(data);
        vm.Problems.Should().ContainSingle();

        vm.CommitField(vm.Rows[0], RegionField.RegionName, "named now").IsValid.Should().BeTrue();

        vm.Problems.Should().BeEmpty();
    }

    [Fact]
    public void ARefusedEditThatWasNeverStored_StaysOnTheRowAndOutOfTheReport()
    {
        // The boundary the report is drawn at: text the model REFUSED is not in the data. It is
        // one keystroke or one Esc away from being gone, so counting it would make the panel
        // churn while the user typed and would flip the count on a revert. The row is flagged --
        // that is where an in-flight problem is shown -- and the report stays quiet.
        var (vm, row, region) = OneRow();

        vm.CommitField(row, RegionField.RegionName, "").IsValid.Should().BeFalse();

        row.HasError.Should().BeTrue();
        row.ErrorText.Should().Be("Region Name is required.");
        region.RegionName.Should().Be("region", "nothing was written");
        vm.Problems.Should().BeEmpty();

        // and giving up on the edit does not make one appear either.
        vm.RevertField(row, RegionField.RegionName);
        vm.Problems.Should().BeEmpty();
    }

    /// <summary>A region exported as a typed asset whose stored values are all legal, so it can
    /// only ever contribute a problem that is about its RELATIONSHIP to another region.</summary>
    private static Region AssetRegion(string name, int start, int end)
    {
        var region = NewRegion(name, start, end);
        region.ExportType = RegionExportType.Asset;
        region.AssetType = "gfx.snes.2bpp"; // 0x10 bytes per tile
        return region;
    }

    private static List<string> ErrorMessages(RegionListViewModel vm) =>
        vm.Problems.Where(p => p.Severity == RegionProblemSeverity.Error).Select(p => p.Message).ToList();

    // =========================================================================================
    // reporting data changes to the host
    // =========================================================================================

    [Fact]
    public void RegionsChanged_IsRaisedByAnAdd()
    {
        var data = NewData();
        using var vm = new RegionListViewModel(data);
        var count = 0;
        vm.RegionsChanged += (_, _) => count++;

        vm.AddRegion();

        count.Should().Be(1);
    }

    [Fact]
    public void RegionsChanged_IsRaisedByADelete()
    {
        var data = NewData(NewRegion());
        using var vm = new RegionListViewModel(data);
        var count = 0;
        vm.RegionsChanged += (_, _) => count++;

        vm.DeleteRegion(vm.Rows[0]);

        count.Should().Be(1);
    }

    [Fact]
    public void RegionsChanged_IsRaisedByACommittedEdit()
    {
        var (vm, row, _) = OneRow();
        var count = 0;
        vm.RegionsChanged += (_, _) => count++;

        vm.CommitField(row, RegionField.RegionName, "renamed");

        count.Should().Be(1);
    }

    [Fact]
    public void RegionsChanged_IsNotRaisedByARefusedEdit()
    {
        var (vm, row, _) = OneRow();
        var count = 0;
        vm.RegionsChanged += (_, _) => count++;

        vm.CommitField(row, RegionField.RegionName, "").IsValid.Should().BeFalse();

        count.Should().Be(0);
    }

    [Fact]
    public void RegionsChanged_IsNotRaisedByReSorting()
    {
        // sorting is a display choice; it must not mark the project as having unsaved changes.
        var data = NewData(NewRegion("a", 0x100, 0x100), NewRegion("b", 0x200, 0x200));
        using var vm = new RegionListViewModel(data);
        var count = 0;
        vm.RegionsChanged += (_, _) => count++;

        vm.SortField = RegionField.RegionName;
        vm.SortDescending = true;
        vm.SelectedRow = vm.Rows[0];

        count.Should().Be(0);
    }

    [Fact]
    public void RegionsChanged_IsNotRaisedWhenTheValueDidNotActuallyChange()
    {
        var (vm, row, _) = OneRow();
        var count = 0;
        vm.RegionsChanged += (_, _) => count++;

        vm.CommitField(row, RegionField.Start, "808000").IsValid.Should().BeTrue();

        count.Should().Be(0);
    }

    [Fact]
    public void RegionsChanged_IsNotRaisedByABlankIgnoredEdit()
    {
        var (vm, row, _) = OneRow();
        var count = 0;
        vm.RegionsChanged += (_, _) => count++;

        vm.CommitField(row, RegionField.Start, "");

        count.Should().Be(0);
    }

    // =========================================================================================
    // the thread rule: every notification routes through the injected marshaller
    // =========================================================================================

    [Fact]
    public void ModelDrivenNotifications_RouteThroughTheInjectedMarshaller()
    {
        var data = NewData(NewRegion("a", 0x100, 0x100));
        var queue = new List<Action>();
        using var vm = new RegionListViewModel(data, queue.Add);
        Drain(queue);
        queue.Clear();

        var propertyEvents = new List<string>();
        var collectionEvents = new List<NotifyCollectionChangedEventArgs>();
        vm.PropertyChanged += (_, e) => propertyEvents.Add(e.PropertyName);
        ((INotifyCollectionChanged)vm.Rows).CollectionChanged += (_, e) => collectionEvents.Add(e);

        data.Regions.Add(NewRegion("b", 0x200, 0x200));

        // nothing may have reached us yet: the change is parked in the marshaller.
        vm.Rows.Should().ContainSingle();
        propertyEvents.Should().BeEmpty();
        collectionEvents.Should().BeEmpty();
        queue.Should().NotBeEmpty();

        Drain(queue);

        vm.Rows.Should().HaveCount(2);
        collectionEvents.Should().Contain(e => e.Action == NotifyCollectionChangedAction.Add);
        propertyEvents.Should().Contain(nameof(vm.RegionCount));
    }

    [Fact]
    public void RowNotifications_AlsoRouteThroughTheMarshaller()
    {
        var region = NewRegion("a", 0x100, 0x100);
        var data = NewData(region);
        var queue = new List<Action>();
        using var vm = new RegionListViewModel(data, queue.Add);
        Drain(queue);
        var row = vm.Rows[0];

        var raised = new List<string>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        queue.Clear();

        region.RegionName = "renamed";

        raised.Should().BeEmpty("the row's relay of the region's own notification must be marshalled too");
        Drain(queue);
        raised.Should().Contain(nameof(RegionRowViewModel.RegionNameText));
    }

    [Fact]
    public void CommandDrivenRowChanges_AreAppliedSynchronously_EvenUnderADeferringMarshaller()
    {
        // The deliberate other half of the thread rule, matching the label editor: commands are
        // contracted to run on the UI thread and update the row collection before they return,
        // so a caller can use the row it just created. Only the notifications are marshalled.
        var data = NewData();
        var queue = new List<Action>();
        using var vm = new RegionListViewModel(data, queue.Add);
        Drain(queue);
        queue.Clear();

        var row = vm.AddRegion();

        vm.Rows.Should().ContainSingle().Which.Should().BeSameAs(row);
        data.Regions.Should().ContainSingle();
    }

    [Fact]
    public void CommandDrivenSortingAndDeleting_AreAlsoAppliedSynchronously()
    {
        var a = NewRegion("a", 0x100, 0x100);
        var b = NewRegion("b", 0x200, 0x200);
        var data = NewData(a, b);
        var queue = new List<Action>();
        using var vm = new RegionListViewModel(data, queue.Add);
        Drain(queue);
        queue.Clear();

        vm.SortDescending = true;
        vm.Rows.Select(r => r.UnderlyingRegion).Should().Equal(b, a);

        vm.DeleteRegion(vm.Rows[0]);
        vm.Rows.Select(r => r.UnderlyingRegion).Should().Equal(a);
    }

    // =========================================================================================
    // giving up on an edit (RevertField)
    // =========================================================================================

    [Fact]
    public void RevertingARefusedField_DropsTheTypedTextAndTheMarkerWithIt()
    {
        var (vm, row, region) = OneRow();
        vm.CommitField(row, RegionField.Start, "!!!").IsValid.Should().BeFalse();
        row.HasError.Should().BeTrue();

        vm.RevertField(row, RegionField.Start);

        row.StartText.Should().Be("808000", "the field shows the value the region actually holds");
        row.HasPendingTextFor(RegionField.Start).Should().BeFalse();
        row.HasError.Should().BeFalse();
        row.ErrorText.Should().Be("");
        region.StartSnesAddress.Should().Be(0x808000, "reverting writes nothing");
    }

    [Fact]
    public void RevertingOneField_LeavesAnotherFieldsRefusalAlone()
    {
        var (vm, row, _) = OneRow();
        vm.CommitField(row, RegionField.Start, "!!!");
        vm.CommitField(row, RegionField.Priority, "high");

        vm.RevertField(row, RegionField.Start);

        row.StartText.Should().Be("808000");
        row.PriorityText.Should().Be("high", "the other field is still showing what the user typed");
        row.HasError.Should().BeTrue("that field's refusal is still outstanding");
        row.ErrorText.Should().Be("Priority must be a valid number.");
    }

    [Fact]
    public void RevertingAField_DoesNotRevalidate_SoAnAlreadyBrokenRowGainsNoNewError()
    {
        // Existing projects carry regions whose STORED values break a rule -- here an asset type
        // no descriptor owns. Backing out of an unrelated edit must leave that row exactly the
        // error it already had, and must not re-attribute it to the field being abandoned.
        var region = NewRegion("r", 0x808000, 0x80800F);
        region.ExportType = RegionExportType.Asset;
        region.AssetType = "gfx.snes.9bpp";
        var (vm, row, _) = OneRow(region);

        var errorBefore = row.ErrorText;
        errorBefore.Should().NotBe("", "the stored values already fail a rule");

        vm.CommitField(row, RegionField.RegionName, "").IsValid.Should().BeFalse();
        vm.RevertField(row, RegionField.RegionName);

        row.HasPendingTextFor(RegionField.RegionName).Should().BeFalse();
        row.RegionNameText.Should().Be("r");
        row.HasError.Should().BeTrue("the stored values still break a rule");
        row.ErrorText.Should().Be(errorBefore, "and it is still the SAME complaint");
    }

    [Fact]
    public void RevertingAFieldNobodyEdited_ChangesNothing()
    {
        var (vm, row, region) = OneRow();

        vm.RevertField(row, RegionField.RegionName);

        row.RegionNameText.Should().Be("region");
        row.HasError.Should().BeFalse();
        region.RegionName.Should().Be("region");
    }

    [Fact]
    public void RevertingAField_DoesNotReportRegionDataAsChanged()
    {
        // nothing was written, so nothing is newly unsaved.
        var (vm, row, _) = OneRow();
        vm.CommitField(row, RegionField.Start, "!!!");

        var changed = 0;
        vm.RegionsChanged += (_, _) => changed++;
        vm.RevertField(row, RegionField.Start);

        changed.Should().Be(0);
    }

    [Fact]
    public void RevertingAFieldOfARowFromADifferentList_IsRefused()
    {
        var (vm, _, _) = OneRow();
        var (_, foreignRow, _) = OneRow();

        var revert = () => vm.RevertField(foreignRow, RegionField.Start);

        revert.Should().Throw<ArgumentException>();
    }

    private static void Drain(List<Action> queue)
    {
        // nested marshalled actions join the queue, so walk it by index rather than snapshotting.
        for (var i = 0; i < queue.Count; i++)
            queue[i]();
    }

    // =========================================================================================
    // lifecycle
    // =========================================================================================

    [Fact]
    public void ADisposedViewModel_StopsWatchingTheRegionCollection()
    {
        var data = NewData(NewRegion("a", 0x100, 0x100));
        var vm = new RegionListViewModel(data);
        vm.Dispose();

        data.Regions.Add(NewRegion("b", 0x200, 0x200));

        vm.Rows.Should().BeEmpty();
    }
}
