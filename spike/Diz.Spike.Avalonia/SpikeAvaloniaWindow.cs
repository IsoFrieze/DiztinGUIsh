using System.Collections.Generic;
using Avalonia.Controls;

namespace Diz.Spike.Avalonia;

/// <summary>
/// HOSTING MODEL (a): a real Avalonia top-level Window, opened from the running
/// WinForms app, in the same process. Avalonia owns this HWND entirely.
/// </summary>
public sealed class SpikeAvaloniaWindow : Window
{
    public LabelListView Virtualizing { get; }
    public NaiveLabelListView Naive { get; }
    public TabControl Tabs { get; }

    public SpikeAvaloniaWindow(List<LabelRow> rows)
    {
        Title = "Diz spike (a) -- Avalonia top-level window";
        Width = 700;
        Height = 600;

        Virtualizing = new LabelListView(rows, "model a: Avalonia top-level Window");
        Naive = new NaiveLabelListView(rows);

        Tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Virtualizing ListBox", Content = Virtualizing },
                new TabItem { Header = "Naive ItemsControl", Content = Naive },
            },
        };
        Content = Tabs;

        Opened += (_, _) => SpikeLog.Write("model (a) window Opened");
        Closed += (_, _) => SpikeLog.Write("model (a) window Closed");
    }
}
