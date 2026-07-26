using Diz.Core.commands;

namespace Diz.Ui.ViewModels.MarkMany;

/// <summary>
/// Snapshot of a <see cref="MarkManyViewModel{TDataSource}"/>'s value choices, so a host can
/// restore what the user picked last time. Plain data: no behavior, no framework, safe to
/// hold in a long-lived host object for the lifetime of a session.
///
/// <see cref="AllSettings"/> holds one boxed value per property, whose runtime type matches
/// what <see cref="MarkCommand.Value"/> needs for that property (FlagType / int / bool /
/// Architecture). Restoring skips entries that don't fit rather than throwing, so an old or
/// hand-built snapshot can never break the editor.
/// </summary>
public sealed class MarkManySettings
{
    public Dictionary<MarkCommand.MarkManyProperty, object> AllSettings { get; set; } = new();

    public MarkCommand.MarkManyProperty SelectedProperty { get; set; } = MarkCommand.MarkManyProperty.Flag;
}
