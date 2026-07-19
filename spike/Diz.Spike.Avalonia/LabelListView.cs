using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace Diz.Spike.Avalonia;

/// <summary>
/// The shared Avalonia control used by BOTH hosting models, so the two models
/// differ only in how they are hosted -- not in what they display.
/// </summary>
public sealed class LabelListView : UserControl
{
    private readonly ListBox listBox;
    private readonly TextBlock statusText;
    private readonly List<LabelRow> allRows;

    public LabelListView(List<LabelRow> rows, string hostingModelName)
    {
        allRows = rows;

        statusText = new TextBlock
        {
            Margin = new Thickness(8, 6),
            FontWeight = FontWeight.Bold,
            Text = $"[{hostingModelName}] {rows.Count:N0} labels loaded",
        };

        var filterBox = new TextBox
        {
            Margin = new Thickness(8, 0, 8, 6),
            Watermark = "filter labels...",
        };
        filterBox.TextChanged += (_, _) => ApplyFilter(filterBox.Text);

        listBox = new ListBox
        {
            ItemsSource = rows,
            // Avalonia 11/12 ListBox already defaults to VirtualizingStackPanel.
            // Stated explicitly here so the spike is unambiguous about what it measured.
            ItemsPanel = new FuncTemplate<Panel>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<LabelRow>((_, _) =>
                new TextBlock
                {
                    [!TextBlock.TextProperty] = new Binding(nameof(LabelRow.Display)),
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                }, supportsRecycling: true),
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
        };
        Grid.SetRow(statusText, 0);
        Grid.SetRow(filterBox, 1);
        Grid.SetRow(listBox, 2);
        grid.Children.Add(statusText);
        grid.Children.Add(filterBox);
        grid.Children.Add(listBox);

        Content = grid;
    }

    private void ApplyFilter(string text)
    {
        var sw = Stopwatch.StartNew();
        var filtered = string.IsNullOrWhiteSpace(text)
            ? allRows
            : allRows.Where(r => r.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        listBox.ItemsSource = filtered;
        sw.Stop();
        SpikeLog.Write($"filter '{text}' -> {filtered.Count:N0} rows in {sw.Elapsed.TotalMilliseconds:F1} ms");
    }

    /// <summary>
    /// The actual virtualization measurement. If the panel is virtualizing,
    /// realized container count stays proportional to the viewport (tens),
    /// not to the item count (thousands).
    /// </summary>
    public string DescribeVirtualization()
    {
        var realized = listBox.GetRealizedContainers()?.Count() ?? -1;
        var panel = listBox.ItemsPanelRoot?.GetType().Name ?? "<null>";
        var total = (listBox.ItemsSource as System.Collections.ICollection)?.Count ?? -1;
        return $"panel={panel} totalItems={total} realizedContainers={realized}";
    }

    public void ScrollStressTest()
    {
        var total = allRows.Count;
        var sw = Stopwatch.StartNew();
        var steps = 0;

        // Jump the scroll around the whole list and force layout each time.
        foreach (var frac in new[] { 0.0, 0.25, 0.5, 0.75, 0.999, 0.5, 0.0 })
        {
            var idx = (int)(total * frac);
            if (idx >= total) idx = total - 1;
            listBox.ScrollIntoView(idx);
            listBox.UpdateLayout();
            steps++;
            SpikeLog.Write($"  scrolled to item {idx} ({frac:P0}) -> {DescribeVirtualization()}");
        }

        sw.Stop();
        SpikeLog.Write($"ScrollStressTest: {steps} full-range scroll+layout passes over {total:N0} rows in {sw.Elapsed.TotalMilliseconds:F1} ms " +
                       $"(avg {sw.Elapsed.TotalMilliseconds / steps:F1} ms/pass)");
    }

    public Control ListBoxControl => listBox;

    /// <summary>Filtering half of the Phase 3 perf gate.</summary>
    public void FilterStressTest()
    {
        foreach (var term in new[] { "SNES", "r_", "overw", "zzz_no_match_zzz", "" })
        {
            var sw = Stopwatch.StartNew();
            ApplyFilter(term);
            listBox.UpdateLayout();
            sw.Stop();
            SpikeLog.Write($"  filter+relayout '{term}' total {sw.Elapsed.TotalMilliseconds:F1} ms -> {DescribeVirtualization()}");
        }
    }
}
