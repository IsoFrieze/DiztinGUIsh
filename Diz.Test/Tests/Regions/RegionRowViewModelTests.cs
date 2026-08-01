using System;
using System.Collections.Generic;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using Diz.Ui.ViewModels.Regions;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.Regions;

/// <summary>
/// One region row: how it renders a region as text, how it derives length, and the split
/// between the value the model holds and the text the user typed that the model refused.
/// </summary>
public class RegionRowViewModelTests
{
    private static Region NewRegion(int start = 0x808000, int end = 0x80800F, string name = "region") =>
        new()
        {
            StartSnesAddress = start,
            EndSnesAddress = end,
            RegionName = name,
        };

    private static (RegionListViewModel vm, RegionRowViewModel row, Region region) NewRowUnderAList(
        Region region = null)
    {
        var model = region ?? NewRegion();
        var data = new Data();
        data.Regions.Add(model);
        var vm = new RegionListViewModel(data);
        return (vm, (RegionRowViewModel)vm.Rows[0], model);
    }

    private static List<string> RecordNotifications(RegionRowViewModel row)
    {
        var raised = new List<string>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }

    // =========================================================================================
    // identity + rendering
    // =========================================================================================

    [Fact]
    public void Identity_IsTheRegionInstanceItself()
    {
        var region = NewRegion();
        var row = new RegionRowViewModel(region);

        row.UnderlyingRegion.Should().BeSameAs(region);
    }

    [Fact]
    public void Addresses_RenderAsSixUppercaseHexDigits()
    {
        var row = new RegionRowViewModel(NewRegion(0x00C123, 0x80800F));

        row.StartText.Should().Be("00C123");
        row.EndText.Should().Be("80800F");
    }

    [Fact]
    public void Length_RendersAsUnpaddedHex()
    {
        new RegionRowViewModel(NewRegion(0x808000, 0x80800F)).LengthText.Should().Be("10");
        new RegionRowViewModel(NewRegion(0x808000, 0x808000)).LengthText.Should().Be("1");
        new RegionRowViewModel(NewRegion(0x808000, 0x8080FF)).LengthText.Should().Be("100");
    }

    [Fact]
    public void Length_CountsTheEndByte_SoAOneByteRegionIsLengthOne()
    {
        var row = new RegionRowViewModel(NewRegion(0x808000, 0x808000));

        row.RegionLength.Should().Be(1);
    }

    [Fact]
    public void EveryFieldHasATextRendering()
    {
        var region = new Region
        {
            StartSnesAddress = 0x808000,
            EndSnesAddress = 0x80801F,
            RegionName = "font",
            ContextToApply = "Battle",
            Priority = 7,
            ExportSeparateFile = true,
            ExportType = RegionExportType.Asset,
            AssetType = "gfx.snes.4bpp",
            AssetVersion = "v1",
            AssetName = "gfx/font",
            AssetOptions = "{\"cell_h\": 12}",
        };

        var row = new RegionRowViewModel(region);

        row.StartText.Should().Be("808000");
        row.EndText.Should().Be("80801F");
        row.LengthText.Should().Be("20");
        row.RegionNameText.Should().Be("font");
        row.ContextToApplyText.Should().Be("Battle");
        row.PriorityText.Should().Be("7");
        row.ExportSeparateFileText.Should().Be("True");
        row.ExportTypeText.Should().Be("Asset");
        row.AssetTypeText.Should().Be("gfx.snes.4bpp");
        row.AssetVersionText.Should().Be("v1");
        row.AssetNameText.Should().Be("gfx/font");
        row.AssetOptionsText.Should().Be("{\"cell_h\": 12}");
    }

    [Fact]
    public void UnsetStringFields_RenderAsEmpty_NotNull()
    {
        // a freshly created region has null strings; nothing in a view should ever see null.
        var row = new RegionRowViewModel(new Region());

        row.RegionNameText.Should().Be("");
        row.ContextToApplyText.Should().Be("");
        row.AssetTypeText.Should().Be("");
        row.AssetVersionText.Should().Be("");
        row.AssetNameText.Should().Be("");
        row.AssetOptionsText.Should().Be("");
    }

    [Fact]
    public void TypedAccessorsExposeTheCommittedValues()
    {
        var region = NewRegion();
        region.ExportSeparateFile = true;
        region.ExportType = RegionExportType.Binary;

        var row = new RegionRowViewModel(region);

        row.ExportSeparateFile.Should().BeTrue();
        row.ExportType.Should().Be(RegionExportType.Binary);
    }

    // =========================================================================================
    // asset fields are enabled only when the bytes are not plain inline assembly
    // =========================================================================================

    [Theory]
    [InlineData(RegionExportType.Assembly, false)]
    [InlineData(RegionExportType.Binary, true)]
    [InlineData(RegionExportType.Asset, true)]
    public void AssetFieldsEnabled_FollowsTheExportType(RegionExportType exportType, bool expected)
    {
        var region = NewRegion();
        region.ExportType = exportType;

        new RegionRowViewModel(region).AssetFieldsEnabled.Should().Be(expected);
    }

    [Fact]
    public void DisablingTheAssetFields_DoesNotClearTheStoredAssetValues()
    {
        var region = NewRegion();
        region.ExportType = RegionExportType.Asset;
        region.AssetType = "gfx.snes.4bpp";
        var row = new RegionRowViewModel(region);

        region.ExportType = RegionExportType.Assembly;

        row.AssetFieldsEnabled.Should().BeFalse();
        row.AssetTypeText.Should().Be("gfx.snes.4bpp", "flipping back must restore what was typed");
    }

    // =========================================================================================
    // the row relays the region's own change notification
    // =========================================================================================

    [Fact]
    public void AStartAddressChange_RepaintsBothTheAddressAndTheLength()
    {
        var region = NewRegion();
        var row = new RegionRowViewModel(region);
        var raised = RecordNotifications(row);

        region.StartSnesAddress = 0x808008;

        raised.Should().Contain(nameof(RegionRowViewModel.StartText));
        raised.Should().Contain(nameof(RegionRowViewModel.LengthText));
        row.StartText.Should().Be("808008");
        row.LengthText.Should().Be("8");
    }

    [Fact]
    public void AnEndAddressChange_RepaintsBothTheAddressAndTheLength()
    {
        var region = NewRegion();
        var row = new RegionRowViewModel(region);
        var raised = RecordNotifications(row);

        region.EndSnesAddress = 0x80801F;

        raised.Should().Contain(nameof(RegionRowViewModel.EndText));
        raised.Should().Contain(nameof(RegionRowViewModel.LengthText));
        row.LengthText.Should().Be("20");
    }

    [Fact]
    public void AnExportTypeChange_RepaintsTheAssetFieldEnableState()
    {
        var region = NewRegion();
        var row = new RegionRowViewModel(region);
        var raised = RecordNotifications(row);

        region.ExportType = RegionExportType.Asset;

        raised.Should().Contain(nameof(RegionRowViewModel.AssetFieldsEnabled));
        raised.Should().Contain(nameof(RegionRowViewModel.ExportType));
        raised.Should().Contain(nameof(RegionRowViewModel.ExportTypeText));
    }

    [Fact]
    public void EveryEditableField_RelaysItsOwnChange()
    {
        var region = NewRegion();
        var row = new RegionRowViewModel(region);
        var raised = RecordNotifications(row);

        region.RegionName = "renamed";
        region.ContextToApply = "Battle";
        region.Priority = 3;
        region.ExportSeparateFile = true;
        region.AssetType = "gfx.snes.2bpp";
        region.AssetVersion = "v2";
        region.AssetName = "gfx/x";
        region.AssetOptions = "{}";

        raised.Should().Contain(new[]
        {
            nameof(RegionRowViewModel.RegionNameText),
            nameof(RegionRowViewModel.ContextToApplyText),
            nameof(RegionRowViewModel.PriorityText),
            nameof(RegionRowViewModel.ExportSeparateFileText),
            nameof(RegionRowViewModel.AssetTypeText),
            nameof(RegionRowViewModel.AssetVersionText),
            nameof(RegionRowViewModel.AssetNameText),
            nameof(RegionRowViewModel.AssetOptionsText),
        });
    }

    [Fact]
    public void ADisposedRow_StopsRelayingTheRegion()
    {
        var region = NewRegion();
        var row = new RegionRowViewModel(region);
        row.Dispose();
        var raised = RecordNotifications(row);

        region.RegionName = "renamed";

        raised.Should().BeEmpty();
    }

    // =========================================================================================
    // last-good value vs. the text the user typed
    // =========================================================================================

    [Fact]
    public void ARejectedEdit_ShowsTheTypedText_WhileTheCommittedValueIsUnchanged()
    {
        var (vm, row, region) = NewRowUnderAList();

        vm.CommitField(row, RegionField.Start, "not an address");

        row.TextFor(RegionField.Start).Should().Be("not an address");
        row.StartText.Should().Be("not an address");
        row.LastGoodTextFor(RegionField.Start).Should().Be("808000");
        row.HasPendingTextFor(RegionField.Start).Should().BeTrue();
        region.StartSnesAddress.Should().Be(0x808000);
    }

    [Fact]
    public void OtherFields_AreUnaffectedByOneFieldsPendingText()
    {
        var (vm, row, _) = NewRowUnderAList();

        vm.CommitField(row, RegionField.Start, "nope");

        row.HasPendingTextFor(RegionField.End).Should().BeFalse();
        row.EndText.Should().Be(row.LastGoodTextFor(RegionField.End));
    }

    [Fact]
    public void CorrectingARejectedEdit_DropsTheTypedTextAndShowsTheCommittedValue()
    {
        var (vm, row, region) = NewRowUnderAList();
        vm.CommitField(row, RegionField.Start, "nope");

        vm.CommitField(row, RegionField.Start, "808004");

        row.HasPendingTextFor(RegionField.Start).Should().BeFalse();
        row.StartText.Should().Be("808004");
        region.StartSnesAddress.Should().Be(0x808004);
    }

    [Fact]
    public void ACommittedChangeElsewhere_ClearsStaleTypedText()
    {
        var (vm, row, region) = NewRowUnderAList();
        vm.CommitField(row, RegionField.Length, "0");   // refused: length must be at least 1

        row.HasPendingTextFor(RegionField.Length).Should().BeTrue();

        // moving an address recomputes the length, so whatever was typed there is history.
        region.EndSnesAddress = 0x80801F;

        row.HasPendingTextFor(RegionField.Length).Should().BeFalse();
        row.LengthText.Should().Be("20");
    }

    // =========================================================================================
    // the visible bad-row marker
    // =========================================================================================

    [Fact]
    public void AValidRow_IsNotFlagged()
    {
        var (_, row, _) = NewRowUnderAList();

        row.HasError.Should().BeFalse();
        row.ErrorText.Should().Be("");
    }

    [Fact]
    public void ARejectedEdit_FlagsTheRowWithItsMessage()
    {
        var (vm, row, _) = NewRowUnderAList();
        var raised = RecordNotifications(row);

        vm.CommitField(row, RegionField.RegionName, "  ");

        row.HasError.Should().BeTrue();
        row.ErrorText.Should().Be("Region Name is required.");
        raised.Should().Contain(nameof(RegionRowViewModel.HasError));
        raised.Should().Contain(nameof(RegionRowViewModel.ErrorText));
    }

    [Fact]
    public void ARowLoadedInvalid_IsFlaggedImmediately()
    {
        // an existing project can contain a region no rule would accept today; it must be
        // visible as a problem, not silently repaired and not blocking.
        var broken = NewRegion(0x808010, 0x808000);
        var (_, row, _) = NewRowUnderAList(broken);

        row.HasError.Should().BeTrue();
        row.ErrorText.Should().Be("Start address must not be greater than end address.");
    }

    [Fact]
    public void FixingTheOffendingField_ClearsTheFlag()
    {
        var (vm, row, _) = NewRowUnderAList(NewRegion(0x808010, 0x808000));

        vm.CommitField(row, RegionField.End, "808020");

        row.HasError.Should().BeFalse();
        row.ErrorText.Should().Be("");
    }
}
