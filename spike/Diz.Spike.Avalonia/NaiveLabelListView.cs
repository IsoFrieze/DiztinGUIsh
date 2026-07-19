using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;

namespace Diz.Spike.Avalonia;

/// <summary>
/// The deliberately BAD control: an ItemsControl forced onto a plain StackPanel,
/// i.e. no virtualization. Exists purely as the control group for the perf gate
/// in docs/new-ui-plan.md Phase 3 -- so the "you must virtualize" claim is
/// measured here rather than assumed.
/// </summary>
public sealed class NaiveLabelListView : UserControl
{
    private readonly ItemsControl itemsControl;

    public NaiveLabelListView(List<LabelRow> rows)
    {
        itemsControl = new ItemsControl
        {
            ItemsSource = rows,
            ItemsPanel = new FuncTemplate<Panel>(() => new StackPanel()), // NOT virtualizing
            ItemTemplate = new FuncDataTemplate<LabelRow>((_, _) =>
                new TextBlock
                {
                    [!TextBlock.TextProperty] = new Binding(nameof(LabelRow.Display)),
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                }),
        };

        Content = new ScrollViewer { Content = itemsControl };
    }

    public void MeasureRealization()
    {
        var sw = Stopwatch.StartNew();
        itemsControl.UpdateLayout();
        sw.Stop();

        var panel = itemsControl.ItemsPanelRoot;
        var realized = panel?.Children.Count ?? -1;
        SpikeLog.Write($"NAIVE (StackPanel) ItemsControl: panel={panel?.GetType().Name} " +
                       $"realizedChildren={realized} layoutPass={sw.Elapsed.TotalMilliseconds:F1} ms");
    }
}
