using Diz.Core.util;

namespace Diz.Ui.ViewModels.ImportRom;

/// <summary>
/// One row of the interrupt-vector table on the ROM import screen: the vector's name, the word
/// currently sitting in that slot, and whether a label should be generated for it.
///
/// <see cref="Name"/> never changes -- it identifies the row, and is the name the generated
/// label will carry. Everything else is recomputed whenever the selected ROM map mode changes,
/// because the vector table lives at a different ROM offset under each mapping.
///
/// ALWAYS-ENABLED ROWS ARE NOT A BUG -- SEE <see cref="IsAlwaysEnabled"/>.
/// </summary>
public sealed class SnesVectorRowViewModel : ViewModelNotifierBase
{
    private string displayValue;
    private bool isEnabled;
    private bool isSelectable;

    /// <param name="name">Canonical vector name; also the generated label's name.</param>
    /// <param name="displayValue">The slot's current contents, already rendered for display.</param>
    /// <param name="isEnabled">Whether a label is generated for this vector.</param>
    /// <param name="isSelectable">Whether the user may change <paramref name="isEnabled"/>.</param>
    /// <param name="isAlwaysEnabled">See <see cref="IsAlwaysEnabled"/>.</param>
    /// <param name="notificationMarshaller">See <see cref="ViewModelNotifierBase"/>.</param>
    public SnesVectorRowViewModel(
        string name,
        string displayValue,
        bool isEnabled,
        bool isSelectable,
        bool isAlwaysEnabled = false,
        Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
        IsAlwaysEnabled = isAlwaysEnabled;
        this.displayValue = displayValue ?? "";
        this.isEnabled = isAlwaysEnabled || isEnabled;
        this.isSelectable = !isAlwaysEnabled && isSelectable;
    }

    /// <summary>
    /// Canonical vector name, e.g. the native NMI vector. Immutable: it is this row's identity
    /// and the name of the label the import will generate.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// This row's label is generated unconditionally and the user is never offered a way to turn
    /// it off.
    ///
    /// The 65816 vector table has sixteen slots and the SNES only uses some of them; the rest are
    /// reserved by the CPU and go unused on this console. They are still real slots at real ROM
    /// addresses, so labelling them documents the table rather than inventing anything -- which
    /// is why they are emitted even when the ROM could not be analysed at all and every other
    /// row has been switched off. Removing this is a behaviour change, not a cleanup.
    /// </summary>
    public bool IsAlwaysEnabled { get; }

    /// <summary>The word in this vector slot, rendered for display, or the unreadable placeholder.</summary>
    public string DisplayValue
    {
        get => displayValue;
        internal set => this.SetField(ref displayValue, value ?? "");
    }

    /// <summary>
    /// Whether the import should generate a label for this vector. Two-way: this is the row's
    /// checkbox. Writes are ignored while <see cref="IsSelectable"/> is false, which is what
    /// keeps <see cref="IsAlwaysEnabled"/> rows on and unreadable rows off.
    /// </summary>
    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (!IsSelectable)
                return;

            this.SetField(ref isEnabled, value);
        }
    }

    /// <summary>
    /// Whether the user may change <see cref="IsEnabled"/>. False for rows that are always on,
    /// and false while the slot's value cannot be read at the selected map mode -- there is
    /// nothing meaningful to point a label at in that case.
    /// </summary>
    public bool IsSelectable
    {
        get => isSelectable;
        internal set
        {
            if (IsAlwaysEnabled)
                return;

            this.SetField(ref isSelectable, value);
        }
    }

    /// <summary>
    /// Set <see cref="IsEnabled"/> past the selectable check. For the owning ViewModel only: it
    /// is how a recompute switches unreadable rows off. Always-on rows stay on regardless.
    /// </summary>
    internal void ForceEnabled(bool enabled)
    {
        if (IsAlwaysEnabled && !enabled)
            return;

        this.SetField(ref isEnabled, enabled, propertyName: nameof(IsEnabled));
    }
}
