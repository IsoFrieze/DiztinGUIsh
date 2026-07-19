using System.Collections.Generic;
using Diz.Core.Interfaces;
using Diz.Core.model;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests;

// covers the payloaded ILabelProvider/IReadOnlyLabelProvider.LabelsChanged event added
// alongside the pre-existing payload-less LabelsServiceWithTemp.OnLabelChanged.
public class LabelChangedEventTests
{
    // Data is only stored by the ctor; none of the label operations below touch it.
    private static LabelsServiceWithTemp NewService() => new(null!);

    private static List<LabelChangedEventArgs> Record(LabelsServiceWithTemp svc)
    {
        var events = new List<LabelChangedEventArgs>();
        svc.LabelsChanged += (_, e) => events.Add(e);
        return events;
    }

    [Fact]
    public void AddLabel_NewAddress_RaisesAddedWithThatAddress()
    {
        var svc = NewService();
        var events = Record(svc);

        svc.AddLabel(0x808000, new Label { Name = "foo" });

        events.Should().ContainSingle();
        events[0].Kind.Should().Be(LabelChangeKind.Added);
        events[0].SnesAddress.Should().Be(0x808000);
    }

    [Fact]
    public void AddLabel_ExistingAddress_RaisesReplaced()
    {
        var svc = NewService();
        svc.AddLabel(0x808000, new Label { Name = "foo" });
        var events = Record(svc);

        svc.AddLabel(0x808000, new Label { Name = "bar" }, overwrite: true);

        events.Should().ContainSingle();
        events[0].Kind.Should().Be(LabelChangeKind.Replaced);
        events[0].SnesAddress.Should().Be(0x808000);
    }

    [Fact]
    public void RemoveLabel_RaisesRemovedWithThatAddress()
    {
        var svc = NewService();
        svc.AddLabel(0x808000, new Label { Name = "foo" });
        var events = Record(svc);

        svc.RemoveLabel(0x808000);

        events.Should().ContainSingle();
        events[0].Kind.Should().Be(LabelChangeKind.Removed);
        events[0].SnesAddress.Should().Be(0x808000);
    }

    [Fact]
    public void BulkOperations_RaiseBulkResetWithMinusOne()
    {
        var svc = NewService();
        var events = Record(svc);

        svc.SetAll(new Dictionary<int, IAnnotationLabel> { { 0x808000, new Label { Name = "a" } } });
        svc.AppendLabels(new Dictionary<int, IAnnotationLabel> { { 0x808001, new Label { Name = "b" } } });
        svc.DeleteAllLabels();

        events.Should().HaveCount(3);
        events.Should().OnlyContain(e => e.Kind == LabelChangeKind.BulkReset && e.SnesAddress == -1);
    }

    // the whole point of the change being additive: existing subscribers must be untouched.
    [Fact]
    public void LegacyOnLabelChanged_StillFiresAtEverySite()
    {
        var svc = NewService();
        var legacyCount = 0;
        svc.OnLabelChanged += (_, _) => legacyCount++;

        svc.AddLabel(0x808000, new Label { Name = "foo" });
        svc.RemoveLabel(0x808000);
        svc.SetAll(new Dictionary<int, IAnnotationLabel>());
        svc.AppendLabels(new Dictionary<int, IAnnotationLabel>());
        svc.DeleteAllLabels();

        legacyCount.Should().Be(5);
    }

    // the event must be reachable through the read-only interface, not just the concrete type.
    [Fact]
    public void LabelsChanged_IsVisibleOnReadOnlyInterface()
    {
        IReadOnlyLabelProvider svc = NewService();
        var events = Record((LabelsServiceWithTemp)svc);

        ((ILabelProvider)svc).AddLabel(0x808000, new Label { Name = "foo" });

        events.Should().ContainSingle();
    }
}
