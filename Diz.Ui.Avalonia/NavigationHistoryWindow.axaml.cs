using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Diz.Core.model;
using Diz.Ui.ViewModels.Navigation;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia navigation-history window: a passive view over the SAME
/// <see cref="NavigationHistoryViewModel"/> the WinForms history grid binds, so "what does back do"
/// cannot drift between backends. Nothing in this file decides where to go; it calls ViewModel
/// commands and the ViewModel raises the request.
///
/// RECORDING A POINT MUST NOT NAVIGATE TO IT -- the bug the whole conversion exists to fix -- so
/// there is NO route here from "the grid's selection moved" to "navigate". There is no
/// SelectionChanged handler at all. The grid moves its own selection unbidden: a bound DataGrid
/// puts its caret on row 0 the moment it materializes rows, which for this window means the OLDEST
/// entry in the user's history, and the user is normally on the newest. Only
/// <see cref="HistoryGrid_DoubleTapped"/> and Enter in <see cref="HistoryGrid_KeyDown"/> navigate;
/// both come from real user input and neither can be raised by the grid on its own. Single-clicking
/// a row only selects it.
///
/// THE HIGHLIGHT GOES THE OTHER WAY, off two events: the ViewModel's CurrentIndex, and the row
/// mirror changing. The second is needed because the ViewModel and this window watch the same
/// underlying BindingList and the ViewModel is subscribed FIRST (it is built before any window
/// exists), so when CurrentIndex moves on an append the grid has no such row yet.
///
/// THE ROWS ARE A MIRROR, not the ViewModel's own list: see <see cref="BindingListRows{T}"/> for
/// why a BindingList cannot be bound to a DataGrid directly.
///
/// The window hides on close (mirrors the WinForms host form), so its scroll position and the
/// binding survive reopening. There is no editable text in this window, so none of the typed-text /
/// echo-suppression machinery the region and goto windows carry applies here.
/// </summary>
internal sealed partial class NavigationHistoryWindow : Window
{
    private NavigationHistoryViewModel? vm;
    private BindingListRows<NavigationEntry>? rows;

    public NavigationHistoryWindow()
    {
        InitializeComponent();

        // hide-on-close, exactly like the WinForms host form (this instance and the ViewModel it
        // borrows survive; a later Show() re-shows the same window).
        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };

        // TUNNELING, and wired here rather than as a KeyDown attribute in the markup, because the
        // DataGrid handles Enter ITSELF (it moves the selection down a row) and marks the event
        // handled before a bubbling handler on the grid would ever see it -- measured: the probe
        // that presses Enter recorded zero activations until this became a tunneling handler.
        // Taking it on the way down is also what we want semantically: on this grid Enter means
        // "go to this row", not "next row".
        HistoryGrid.AddHandler(KeyDownEvent, HistoryGrid_KeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Overshoot the ← and → buttons ask for; seeded by the host so the in-window arrows and the
    /// main menu's back/forward mean the same thing. Row activation deliberately keeps
    /// <see cref="NavigationHistoryViewModel.NoOvershoot"/>.
    /// </summary>
    public int BackForwardOvershoot { get; set; } = NavigationHistoryViewModel.NoOvershoot;

    // ------------------------------------------------------------------ VM attach/detach

    public void AttachViewModel(NavigationHistoryViewModel viewModel)
    {
        DetachViewModel();

        vm = viewModel;
        vm.PropertyChanged += Vm_PropertyChanged;

        rows = new BindingListRows<NavigationEntry>(vm.BindableEntries);
        ((INotifyCollectionChanged)rows).CollectionChanged += Rows_CollectionChanged;

        HistoryGrid.ItemsSource = rows;

        // deferred: the grid has not realized its rows yet, and it will put its own caret on row 0
        // when it does. Running after that puts the highlight back where the ViewModel says it is.
        PostSyncSelection();
    }

    public void DetachViewModel()
    {
        HistoryGrid.ItemsSource = null;

        if (rows != null)
        {
            ((INotifyCollectionChanged)rows).CollectionChanged -= Rows_CollectionChanged;
            rows.Dispose();
            rows = null;
        }

        if (vm == null)
            return;

        vm.PropertyChanged -= Vm_PropertyChanged;

        // the ViewModel belongs to the HOST and outlives every window: detach, never dispose.
        vm = null;
    }

    // ------------------------------------------------------------------ commands

    private void BtnBack_Click(object? sender, RoutedEventArgs e) => vm?.MoveBack(BackForwardOvershoot);

    private void BtnForward_Click(object? sender, RoutedEventArgs e) => vm?.MoveForward(BackForwardOvershoot);

    private void BtnClearHistory_Click(object? sender, RoutedEventArgs e) => vm?.Clear();

    // ------------------------------------------------------------------ row activation

    /// <summary>
    /// Double-click a row: go there. Asks even for the row already selected -- double-clicking
    /// where you already are is how the user re-centres the view on it.
    ///
    /// The row is taken from WHAT WAS HIT rather than from the grid's selection, so a double-click
    /// landing on the header or on empty space below the last row activates nothing instead of
    /// re-activating whatever happened to be selected.
    /// </summary>
    private void HistoryGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        // global:: because this assembly's own namespace is Diz.Ui.Avalonia, so a bare `Avalonia.`
        // would resolve to it rather than to the framework (the collision noted in DizAvaloniaApp).
        var row = (e.Source as global::Avalonia.Visual)
            ?.GetSelfAndVisualAncestors().OfType<DataGridRow>().FirstOrDefault();
        Activate(IndexOfRow(row?.DataContext as NavigationEntry));
    }

    /// <summary>Enter activates the selected row: the keyboard equivalent of double-clicking it.</summary>
    private void HistoryGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || vm == null)
            return;

        // the grid's own Enter moves down a row; activating is what the user meant here.
        e.Handled = true;
        Activate(HistoryGrid.SelectedIndex);
    }

    /// <summary>
    /// Go to the row at this position. NoOvershoot: the user pointed at a row, so the view lands
    /// exactly on it -- unlike the arrows, which scroll past their destination.
    /// </summary>
    private void Activate(int index)
    {
        if (index < 0)
            return;

        vm?.SelectEntry(index, NavigationHistoryViewModel.NoOvershoot);
    }

    /// <summary>
    /// Where a row sits in the list the grid is showing, or -1. Reference identity: history entries
    /// are immutable records of a moment and each recording makes a new one, so two entries for the
    /// same address are still two different rows the user can be on.
    /// </summary>
    private int IndexOfRow(NavigationEntry? entry) =>
        entry == null || rows == null ? NavigationHistoryViewModel.NoSelection : rows.IndexOf(entry);

    // ------------------------------------------------------------------ highlight

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavigationHistoryViewModel.CurrentIndex))
            SyncSelectionFromViewModel();
    }

    // the row set changed (an append, or Clear). The ViewModel has already moved CurrentIndex, but
    // it did so before this mirror had the row -- so re-apply it now that the row exists.
    private void Rows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        PostSyncSelection();

    /// <summary>
    /// Re-apply the ViewModel's position DEFERRED. The grid is mid-update over a collection it has
    /// just been told about, and writing SelectedIndex back into it from inside that update
    /// re-enters the same bookkeeping; running afterwards is safe.
    /// </summary>
    private void PostSyncSelection() =>
        Dispatcher.UIThread.Post(SyncSelectionFromViewModel, DispatcherPriority.Background);

    private void SyncSelectionFromViewModel()
    {
        var index = vm?.CurrentIndex ?? NavigationHistoryViewModel.NoSelection;
        if (rows == null || index < 0 || index >= rows.Count)
            return;

        // safe to move freely: nothing in this window turns a selection change into a navigation.
        HistoryGrid.SelectedIndex = index;
        HistoryGrid.ScrollIntoView(rows[index], null);
    }
}
