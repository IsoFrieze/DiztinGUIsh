using System.Collections.Generic;
using System.Windows.Forms;
using Avalonia.Win32.Interoperability;

namespace Diz.Spike.Avalonia;

/// <summary>
/// HOSTING MODEL (b): a normal WinForms Form whose client area contains an
/// Avalonia control, via WinFormsAvaloniaControlHost.
///
/// API NOTE vs the 2021 (0.10.6) code: the type is unchanged in name but MOVED
/// namespace, Avalonia.Win32.Embedding -> Avalonia.Win32.Interoperability.
/// </summary>
public sealed class SpikeEmbeddedForm : Form
{
    private readonly WinFormsAvaloniaControlHost host;
    public LabelListView View { get; }

    public SpikeEmbeddedForm(List<LabelRow> rows)
    {
        Text = "Diz spike (b) -- Avalonia embedded in a WinForms Form";
        Width = 700;
        Height = 600;

        // A WinForms control above the Avalonia host, so we can see whether the
        // two toolkits coexist in one window without clobbering each other.
        var winformsBanner = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "^ this banner is WinForms; everything below is Avalonia",
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        };

        View = new LabelListView(rows, "model b: embedded in WinForms");

        host = new WinFormsAvaloniaControlHost
        {
            Dock = DockStyle.Fill,
            Content = View,
        };

        Controls.Add(host);
        Controls.Add(winformsBanner);

        Shown += (_, _) => SpikeLog.Write("model (b) form Shown");
        FormClosed += (_, _) => SpikeLog.Write("model (b) form FormClosed");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            host?.Dispose();
        base.Dispose(disposing);
    }
}
