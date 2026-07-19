using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

// See TuiLabelEditorView.cs: the static Application API is [Obsolete] in Terminal.Gui v2.4 but is
// the documented lifecycle used by this PoC. Suppress CS0618 locally and deliberately.
#pragma warning disable CS0618

namespace Diz.Ui.Tui;

/// <summary>One immutable snapshot row shown in the TUI list. Decoupled from the live VM row
/// (the PoC takes a one-shot snapshot): jumping resolves <see cref="SnesAddress"/> back to the
/// live VM on the WinForms thread, so a stale snapshot can never touch a disposed row.</summary>
internal readonly record struct TuiLabelRow(string AddressText, string Name, string Comment, int SnesAddress);

/// <summary>
/// The Terminal.Gui screen: a full-window <see cref="ListView"/> of a label snapshot plus a
/// status bar of key hints. Built and run entirely on the Terminal.Gui thread (whichever thread
/// called <see cref="Application.Init()"/>).
///
/// Keys (handled in one place, on the focused ListView's KeyDown):
///   Up/Down/PageUp/PageDown/Home/End  -> left unhandled so ListView's own navigation runs
///   Enter or J                        -> jump to the selected label (marshalled to WinForms)
///   Q or Esc                          -> quit (Application.RequestStop)
///
/// GROWTH PATH (kept intentionally open, per the handoff): the list is bound to an
/// ObservableCollection, so a future reverse-marshalling path (WinForms VM change ->
/// Application.Invoke -> mutate this collection) can live-update the view without restructuring.
/// </summary>
internal sealed class TuiLabelEditorScreen
{
    private readonly IReadOnlyList<TuiLabelRow> rows;
    private readonly Action<int> onJump;
    private readonly Window window;
    private readonly ListView list;

    public TuiLabelEditorScreen(IReadOnlyList<TuiLabelRow> rows, Action<int> onJump)
    {
        this.rows = rows;
        this.onJump = onJump;

        window = new Window
        {
            Title = $"Diz Label Viewer  —  {rows.Count:N0} labels",
        };

        list = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(Dim.Absolute(1)), // leave the bottom line for the status bar
        };

        // ObservableCollection so a future live-update path can mutate it in place.
        var display = new ObservableCollection<string>(rows.Select(FormatRow));
        list.SetSource(display);
        if (rows.Count > 0)
            list.SelectedItem = 0;

        list.KeyDown += OnListKeyDown;

        var status = new StatusBar(new[]
        {
            // display-only hints (null action): all real handling is in OnListKeyDown, which
            // marks keys Handled so nothing double-fires.
            new Shortcut(Key.CursorUp, "Up/Down", null, "Move"),
            new Shortcut(Key.Enter, "Enter/J", null, "Jump to label"),
            new Shortcut(Key.Q, "Q/Esc", null, "Quit"),
        });

        window.Add(list);
        window.Add(status);
    }

    /// <summary>The runnable to hand to <see cref="Application.Run(IRunnable, System.Func{System.Exception, bool})"/>.</summary>
    public Window Window => window;

    /// <summary>Ask the Terminal.Gui main loop to exit Run(). Safe to call from the TUI thread;
    /// from any other thread, wrap in <c>Application.Invoke</c>.</summary>
    public void RequestStop() => Application.RequestStop(window);

    private void OnListKeyDown(object? sender, Key key)
    {
        if (key == Key.Enter || key == Key.J)
        {
            JumpToSelected();
            key.Handled = true;
        }
        else if (key == Key.Q || key == Key.Esc)
        {
            RequestStop();
            key.Handled = true;
        }
        // everything else (cursor movement, paging) is left for ListView's own bindings.
    }

    private void JumpToSelected()
    {
        var index = list.SelectedItem;
        if (index is not { } i || i < 0 || i >= rows.Count)
            return;

        // Hand the address to the host, which marshals to the WinForms thread and drives the
        // SAME navigation path the Avalonia editor uses (VM.NavigationRequested -> SelectOffset).
        onJump(rows[i].SnesAddress);
    }

    private static string FormatRow(TuiLabelRow row)
    {
        var address = Pad(row.AddressText, 8);
        var name = Pad(row.Name, 30);
        var comment = row.Comment.Replace('\n', ' ').Replace('\r', ' ');
        return $"{address}  {name}  {comment}";
    }

    private static string Pad(string value, int width)
    {
        value ??= "";
        if (value.Length >= width)
            return value.Length == width ? value : value[..width];
        return value.PadRight(width);
    }
}
