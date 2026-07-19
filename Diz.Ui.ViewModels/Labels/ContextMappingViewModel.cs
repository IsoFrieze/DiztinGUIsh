using Diz.Core.Interfaces;

namespace Diz.Ui.ViewModels.Labels;

/// <summary>
/// Thin pass-through wrapper over a model IContextMapping. Property writes go straight to the
/// model object (which is itself INPC); the wrapper re-raises its own PropertyChanged through
/// the marshaller so views bound to the VM tree never see an off-thread notification.
/// </summary>
public sealed class ContextMappingViewModel : ViewModelNotifierBase, IContextMappingViewModel, IDisposable
{
    private readonly IContextMapping model;

    public ContextMappingViewModel(IContextMapping model, Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        this.model = model;
        model.PropertyChanged += OnModelPropertyChanged;
    }

    public string Context
    {
        get => model.Context;
        set => model.Context = value; // model raises PropertyChanged; we relay it below
    }

    public string NameOverride
    {
        get => model.NameOverride;
        set => model.NameOverride = value;
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // relay, marshalled. model property names match ours (Context / NameOverride).
        if (e.PropertyName != null)
            OnPropertyChanged(e.PropertyName);
    }

    public void Dispose() => model.PropertyChanged -= OnModelPropertyChanged;
}
