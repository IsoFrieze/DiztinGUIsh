using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;

namespace Diz.Ui.Avalonia;

/// <summary>
/// A live Avalonia-bindable mirror of a <see cref="BindingList{T}"/>.
///
/// WHY THIS EXISTS. BindingList is WinForms' change-notification contract: it raises
/// <see cref="IBindingList.ListChanged"/> and does NOT implement INotifyCollectionChanged, which is
/// the only collection notification an Avalonia ItemsControl understands. Bind a DataGrid straight
/// to a BindingList and it renders whatever was in the list at bind time and then never updates --
/// no error, no warning, just a grid that silently stops agreeing with the model. The navigation
/// history is appended to while its window is open, so that failure would be the normal case.
///
/// IT LIVES IN THE VIEW LAYER, deliberately. The ViewModel's public shape is the shared one -- both
/// backends bind the same NavigationHistoryViewModel, and WinForms genuinely wants the BindingList
/// -- so the adaptation belongs to the toolkit that needs adapting, next to the window that binds
/// it. Nothing here is Avalonia-specific except the dispatcher hop below, which is what confines
/// this file to this assembly.
///
/// READ-ONLY MIRROR. Mutating this collection would not reach the source list; nothing does, and
/// the history is append-only from elsewhere by design (see NavigationHistoryViewModel).
///
/// THREADING. History points can be recorded off the UI thread, so ListChanged can arrive there.
/// Mutating a bound collection from another thread corrupts the grid's bookkeeping, so an
/// off-thread change is posted and applied as a full resync -- by the time it runs, the individual
/// index the event carried may no longer mean anything, but the source list still does.
/// </summary>
/// <typeparam name="T">The row type. Rows are immutable here; only the LIST changes.</typeparam>
internal sealed class BindingListRows<T> : ObservableCollection<T>, IDisposable
{
    private readonly BindingList<T> source;

    public BindingListRows(BindingList<T> source) : base(source)
    {
        ArgumentNullException.ThrowIfNull(source);

        this.source = source;
        source.ListChanged += OnSourceListChanged;
    }

    public void Dispose() => source.ListChanged -= OnSourceListChanged;

    private void OnSourceListChanged(object? sender, ListChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Resync, DispatcherPriority.Background);
            return;
        }

        Apply(e);
    }

    private void Apply(ListChangedEventArgs e)
    {
        var index = e.NewIndex;

        switch (e.ListChangedType)
        {
            case ListChangedType.ItemAdded when InSource(index) && index <= Count:
                Insert(index, source[index]);
                break;

            case ListChangedType.ItemDeleted when index >= 0 && index < Count:
                RemoveAt(index);
                break;

            case ListChangedType.ItemChanged when InSource(index) && index < Count:
                SetItem(index, source[index]);
                break;

            case ListChangedType.ItemMoved when e.OldIndex >= 0 && e.OldIndex < Count
                                                && index >= 0 && index < Count:
                Move(e.OldIndex, index);
                break;

            // Reset (which is what BindingList.Clear() raises), a schema change, or any of the
            // cases above whose indices did not line up with what we are holding. Rebuilding is
            // always correct and the history is small.
            default:
                Resync();
                break;
        }
    }

    /// <summary>Throw away what we hold and re-copy the source.</summary>
    private void Resync()
    {
        Clear();
        foreach (var item in source)
            Add(item);
    }

    private bool InSource(int index) => index >= 0 && index < source.Count;
}
