using System.ComponentModel;
using Diz.Core.util;

namespace Diz.Ui.ViewModels;

/// <summary>
/// Shared base for every ViewModel in this assembly, regardless of feature namespace.
///
/// Hand-rolled INPC base, mirroring the existing Diz pattern (INotifyPropertyChangedExt +
/// NotifyPropertyChangedExtensions.SetField in Diz.Core/util/Util.cs). Deliberately NO
/// ReactiveUI / Fody / any framework: plain INPC only.
///
/// THREAD RULE: every notification this VM layer raises goes through a marshaller injected at
/// construction. In a real host that marshaller posts (or send-if-off-thread) to the UI
/// thread; when none is supplied -- e.g. unit tests -- the default invokes inline,
/// synchronously.
///
/// CONTRACT for hosts: commands are expected to be invoked on the UI thread, and the
/// marshaller MUST execute synchronously when already called on that thread (Send semantics /
/// check-access-then-invoke). A queue-only marshaller is fine for tests but would delay the
/// row bookkeeping that commands rely on.
/// </summary>
public abstract class ViewModelNotifierBase : INotifyPropertyChangedExt
{
    private readonly Action<Action> marshal;

    protected ViewModelNotifierBase(Action<Action>? notificationMarshaller) =>
        marshal = notificationMarshaller ?? (action => action());

    /// <summary>Run an action through the notification marshaller.</summary>
    protected void Marshal(Action action) => marshal(action);

    /// <summary>The raw marshaller, for handing down to child VMs so a whole VM tree shares one.</summary>
    protected Action<Action> NotificationMarshaller => marshal;

    public event PropertyChangedEventHandler? PropertyChanged;

    // public because INotifyPropertyChangedExt requires it (same trade-off the rest of the
    // codebase already makes -- see the comment on the interface itself).
    public void OnPropertyChanged(string propertyName) =>
        marshal(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
}
