using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Ui.ViewModels.Labels;

/// <summary>
/// One label row. Wraps the live model IAnnotationLabel; identity is the SNES address int.
///
/// The row RELAYS the model's own PropertyChanged (the model Label is INPC), so an in-place
/// rename made elsewhere -- e.g. a future details panel bound straight to the Label -- shows
/// up here without any provider event. That covers the known gap from Step 0: in-place
/// renames do not fire the provider's LabelsChanged.
///
/// Lifecycle: the editor VM creates rows and MUST Dispose() them when they leave the row set,
/// or the model label keeps the row (and everything the row references) alive via the event.
/// </summary>
public sealed class LabelRowViewModel : ViewModelNotifierBase, ILabelRowViewModel, IDisposable
{
    private IAnnotationLabel label;

    public LabelRowViewModel(int snesAddress, IAnnotationLabel label, Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        SnesAddress = snesAddress;
        AddressText = Util.ToHexString6(snesAddress);
        this.label = label;
        Attach(label);
        RebuildContextMappingWrappers();
    }

    public int SnesAddress { get; }
    public string AddressText { get; }

    /// <summary>The live model object this row currently wraps. Used by the editor VM for
    /// filtering (LabelSearchTerms wants the model) and for Replaced-event rebinds.</summary>
    internal IAnnotationLabel UnderlyingLabel => label;

    public string Name
    {
        get => label.Name ?? "";
        set
        {
            if (label.Name == value)
                return;
            label.Name = value;
            // the model raises PropertyChanged and we relay it; if a non-INPC label
            // implementation ever appears, still notify.
            if (label is not INotifyPropertyChanged)
                OnPropertyChanged(nameof(Name));
        }
    }

    public string Comment
    {
        get => label.Comment ?? "";
        set
        {
            if (label.Comment == value)
                return;
            label.Comment = value;
            if (label is not INotifyPropertyChanged)
                OnPropertyChanged(nameof(Comment));
        }
    }

    /// <summary>Formatted exactly as the old WinForms "Context" column: mappings with a
    /// whitespace-only Context are skipped; the rest join as "ctx: override, ctx: override".</summary>
    public string ContextSummary =>
        string.Join(", ", label.ContextMappings
            .Where(m => !string.IsNullOrWhiteSpace(m.Context))
            .Select(m => $"{m.Context}: {m.NameOverride}"));

    public ObservableCollection<IContextMappingViewModel> ContextMappings { get; } = [];

    /// <summary>
    /// Swap the underlying model object (a provider "Replaced" means a NEW Label instance now
    /// lives at this address). Raises change notifications for everything displayable.
    /// </summary>
    internal void Rebind(IAnnotationLabel newLabel)
    {
        if (ReferenceEquals(label, newLabel))
            return;

        Detach();
        label = newLabel;
        Attach(newLabel);
        RebuildContextMappingWrappers();

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Comment));
        OnPropertyChanged(nameof(ContextSummary));
    }

    private void Attach(IAnnotationLabel target)
    {
        if (target is INotifyPropertyChanged npc)
            npc.PropertyChanged += OnModelPropertyChanged;
        target.ContextMappings.CollectionChanged += OnModelContextMappingsChanged;
    }

    private void Detach()
    {
        if (label is INotifyPropertyChanged npc)
            npc.PropertyChanged -= OnModelPropertyChanged;
        label.ContextMappings.CollectionChanged -= OnModelContextMappingsChanged;
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IAnnotationLabel.Name):
                OnPropertyChanged(nameof(Name));
                break;
            case nameof(IAnnotationLabel.Comment):
                OnPropertyChanged(nameof(Comment));
                break;
            case nameof(IAnnotationLabel.ContextMappings):
                RebuildContextMappingWrappers();
                OnPropertyChanged(nameof(ContextSummary));
                break;
        }
    }

    private void OnModelContextMappingsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildContextMappingWrappers();
        OnPropertyChanged(nameof(ContextSummary));
    }

    private void RebuildContextMappingWrappers()
    {
        foreach (var wrapper in ContextMappings.OfType<ContextMappingViewModel>())
            wrapper.Dispose();
        ContextMappings.Clear();

        foreach (var mapping in label.ContextMappings)
        {
            var wrapper = new ContextMappingViewModel(mapping, NotificationMarshaller);
            wrapper.PropertyChanged += OnContextWrapperPropertyChanged;
            ContextMappings.Add(wrapper);
        }
    }

    private void OnContextWrapperPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(nameof(ContextSummary));

    public void Dispose()
    {
        Detach();
        foreach (var wrapper in ContextMappings.OfType<ContextMappingViewModel>())
            wrapper.Dispose();
        ContextMappings.Clear();
    }
}
