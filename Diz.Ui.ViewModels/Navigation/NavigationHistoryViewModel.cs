using System.ComponentModel;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Ui.ViewModels.Navigation;

/// <summary>
/// Where to go when the user asks to go BACK: the navigation history, plus which entry in it is
/// the one they are currently sitting on.
///
/// THIS VIEWMODEL NEVER NAVIGATES. It works out a destination and raises
/// <see cref="NavigationRequested"/>; whoever owns it performs the move (ISnesNavigation lives in
/// Diz.Controllers, an assembly this one may not reference). Same separation the goto and
/// misalignment ViewModels use.
///
/// IT MUST OUTLIVE ANY WINDOW. Back/forward are main-window menu commands and work whether or not
/// the history window has ever been opened, so the current position cannot live in a window -- it
/// lives here, and the host constructs this once, up front. That is the per-window navigation
/// contract from the goto conversion, applied to a piece of state that is per-PROJECT-VIEW rather
/// than per-window.
///
/// RECORDING A POINT IS NOT NAVIGATING TO IT (the bug this conversion fixes). Appending to the
/// history moves <see cref="CurrentIndex"/> onto the new entry -- so the next "back" goes to where
/// you were, not to where you already are -- and raises NOTHING. Only <see cref="MoveBack"/>,
/// <see cref="MoveForward"/> and <see cref="SelectEntry"/> ever request a navigation. In the old
/// WinForms control the two were the same code path through BindingSource.Position, so every
/// recorded point re-navigated to itself with no overshoot; see the conversion notes on
/// <see cref="SelectEntry"/> for the overshoot consequence.
///
/// CLAMPED, NOT WRAPPED: back at the oldest entry stays on the oldest entry, forward at the newest
/// stays on the newest -- and still requests a navigation to it, because that is what the WinForms
/// control did and "back" landing you back on screen at your current row is the behaviour users
/// have.
///
/// THE HISTORY LIST IS NOT OURS. It belongs to the document, is appended to by the main window's
/// "remember where I was" path, and is watched here. Nothing in this class adds entries.
/// </summary>
public sealed class NavigationHistoryViewModel : ViewModelNotifierBase, IDisposable
{
    /// <summary><see cref="CurrentIndex"/> when there is no entry to be on.</summary>
    public const int NoSelection = -1;

    /// <summary>
    /// Overshoot for a navigation that should land exactly on the target row. Named so a caller
    /// has to say which it wants: every path through this ViewModel states its overshoot.
    /// </summary>
    public const int NoOvershoot = 0;

    /// <summary>Returned by an address converter that cannot map the address to this ROM.</summary>
    private const int NotInRom = -1;

    private readonly BindingList<NavigationEntry> history;
    private readonly Func<int, int> convertSnesToPc;

    private int currentIndex = NoSelection;

    /// <param name="navigationHistory">
    /// The document's history list, live. Watched for appends and cleared by <see cref="Clear"/>;
    /// never added to here.
    /// </param>
    /// <param name="snesToPcConverter">
    /// Maps a SNES address to a ROM file offset, answering -1 for an address that is not in the
    /// open ROM.
    ///
    /// A DELEGATE rather than an ISnesAddressConverter because of WHEN this ViewModel exists: the
    /// host builds it before any project is open and keeps it across project opens, so there is no
    /// converter instance to capture at construction and any instance captured later would go
    /// stale on the next open. The old control read Document.Project.Data at navigation time for
    /// exactly this reason; the delegate preserves that. (Same caller-seeded-delegate shape as
    /// MisalignmentCheckerViewModel's scan.)
    /// </param>
    /// <param name="notificationMarshaller">See <see cref="ViewModelNotifierBase"/>.</param>
    public NavigationHistoryViewModel(
        BindingList<NavigationEntry> navigationHistory,
        Func<int, int> snesToPcConverter,
        Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        ArgumentNullException.ThrowIfNull(navigationHistory);
        ArgumentNullException.ThrowIfNull(snesToPcConverter);

        history = navigationHistory;
        convertSnesToPc = snesToPcConverter;

        // a list that already has entries in it starts parked on the newest, matching what an
        // append would have left behind.
        currentIndex = LastIndex;

        history.ListChanged += OnHistoryListChanged;
    }

    // ---------------------------------------------------------------- STATE

    /// <summary>The history, oldest first. Read-only here: entries are recorded elsewhere.</summary>
    public IReadOnlyList<NavigationEntry> Entries => history;

    /// <summary>
    /// The list itself, for hosts whose binding stack wants the BindingList (WinForms). Same
    /// object as <see cref="Entries"/>; exposed separately so the read-only intent above is the
    /// one that reads as normal usage.
    /// </summary>
    public BindingList<NavigationEntry> BindableEntries => history;

    public int Count => history.Count;

    /// <summary>
    /// Which entry the user is considered to be on, or <see cref="NoSelection"/> when the history
    /// is empty. Moves on append, on back/forward, and on <see cref="SelectEntry"/>.
    /// </summary>
    public int CurrentIndex
    {
        get => currentIndex;
        private set
        {
            if (currentIndex == value)
                return;

            currentIndex = value;
            OnPropertyChanged(nameof(CurrentIndex));
            OnPropertyChanged(nameof(CurrentEntry));
        }
    }

    /// <summary>The entry <see cref="CurrentIndex"/> names, or null when there is none.</summary>
    public NavigationEntry? CurrentEntry => EntryAt(currentIndex);

    /// <summary>
    /// Raised when the host should navigate. Never raised for anything the user did not ask for:
    /// not on append, not on <see cref="Clear"/>, and not for an entry that does not name a byte
    /// of the open ROM.
    /// </summary>
    public event EventHandler<NavigationRequest>? NavigationRequested;

    // ---------------------------------------------------------------- COMMANDS

    /// <summary>
    /// Go one entry older. Clamped: already on the oldest entry means staying on it and
    /// re-requesting it. Empty history does nothing at all.
    /// </summary>
    /// <param name="overshootAmount">Rows to scroll past the destination; see <see cref="NavigationRequest"/>.</param>
    public void MoveBack(int overshootAmount) => Move(forward: false, overshootAmount);

    /// <summary>Go one entry newer. Clamped and empty-safe, exactly as <see cref="MoveBack"/>.</summary>
    /// <param name="overshootAmount">Rows to scroll past the destination; see <see cref="NavigationRequest"/>.</param>
    public void MoveForward(int overshootAmount) => Move(forward: true, overshootAmount);

    /// <summary>
    /// The user picked an entry out of the list themselves. Moves onto it and requests it.
    ///
    /// This is the path that carries <see cref="NoOvershoot"/> in the WinForms host while
    /// back/forward carry the standard overshoot -- an asymmetry inherited verbatim from the old
    /// control and deliberately NOT resolved here: it is the host that decides, and each caller
    /// now has to say so out loud.
    ///
    /// An index that is not in the list is ignored: no move, no request.
    /// </summary>
    /// <param name="index">Position in <see cref="Entries"/>.</param>
    /// <param name="overshootAmount">Rows to scroll past the destination.</param>
    public void SelectEntry(int index, int overshootAmount)
    {
        if (!IsRealIndex(index))
            return;

        GoTo(index, overshootAmount);
    }

    /// <summary>
    /// Throw the history away. Leaves nothing selected and requests no navigation -- the user is
    /// still looking at wherever they already were.
    /// </summary>
    public void Clear()
    {
        // BindingList.Clear() works even though the document builds this list with
        // AllowRemove = false: that flag guards RemoveItem, not ClearItems (verified against the
        // real BindingList, not assumed). The old control cleared through a BindingSource, which
        // reaches the same ClearItems.
        history.Clear();

        // ListChanged/Reset arrives from that Clear and puts the index at NoSelection; this is
        // belt-and-braces for a list that had already stopped raising events.
        CurrentIndex = LastIndex;
    }

    /// <summary>Stop watching the history list.</summary>
    public void Dispose() => history.ListChanged -= OnHistoryListChanged;

    // ---------------------------------------------------------------- INTERNALS

    private void Move(bool forward, int overshootAmount)
    {
        if (history.Count == 0)
            return;

        // Util.ClampIndex, same call the WinForms control made: this is where "no wrap-around"
        // comes from, and -1 + clamp is why an empty list has to be rejected above.
        var destination = Util.ClampIndex(currentIndex + (forward ? 1 : -1), history.Count);

        GoTo(destination, overshootAmount);
    }

    /// <summary>
    /// Move onto an entry and ask for it. The move happens even when the request cannot: the old
    /// control selected the row whether or not the address converted, and a selection that refused
    /// to move would strand back/forward on an unconvertible entry forever.
    /// </summary>
    private void GoTo(int index, int overshootAmount)
    {
        CurrentIndex = index;

        var pcOffset = ResolvePcOffset(EntryAt(index));
        if (pcOffset == NotInRom)
            return;

        NavigationRequested?.Invoke(this, new NavigationRequest(pcOffset, overshootAmount));
    }

    /// <summary>
    /// The ROM file offset an entry names, or -1 when it names none: either it was recorded with
    /// no real address, or the address does not exist in the ROM that is open now (history
    /// survives closing a project and opening a different one).
    /// </summary>
    private int ResolvePcOffset(NavigationEntry? entry)
    {
        var snesAddress = entry?.SnesOffset ?? NotInRom;
        return snesAddress == NotInRom ? NotInRom : convertSnesToPc(snesAddress);
    }

    /// <summary>
    /// The history grew (or was reset). Park on the newest entry so the next "back" leaves it,
    /// WITHOUT requesting a navigation -- recording where you were is not a reason to go anywhere.
    /// </summary>
    private void OnHistoryListChanged(object? sender, ListChangedEventArgs e) =>
        Marshal(() => CurrentIndex = LastIndex);

    private int LastIndex => history.Count - 1;

    private bool IsRealIndex(int index) => index >= 0 && index < history.Count;

    private NavigationEntry? EntryAt(int index) => IsRealIndex(index) ? history[index] : null;
}

/// <summary>
/// "Go here." A ROM file offset (not a SNES address -- it has already been converted) and how far
/// past it to scroll.
/// </summary>
/// <param name="PcOffset">ROM file offset to land on. Always a real offset; unresolvable entries
/// never produce a request at all.</param>
/// <param name="OvershootAmount">Rows to scroll BEYOND the destination, in the direction of
/// travel, so the destination does not sit against the edge of the grid. 0 lands exactly.</param>
public readonly record struct NavigationRequest(int PcOffset, int OvershootAmount);
