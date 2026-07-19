using Diz.Controllers.interfaces;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia LABEL-EDITOR BACKEND (new-ui plan step 5/6): exactly the three
/// backend-selectable registrations (LabelEditorView, ProgressBarView, IFileDialogService),
/// each named with the exact IViewFactory method-name string. The app registers EITHER this
/// root OR <c>DizUiWinformsBackendCompositionRoot</c> via an explicit if/else branch in
/// DizWinformsRegisterServices when DIZ_LABEL_EDITOR selects a backend -- never both. This
/// replaced the old last-registration-wins ordering trick (step 6). Proven by test:
/// LabelEditorBackendSwitchTests in Diz.App.Winforms.Test.
/// </summary>
[UsedImplicitly]
public class DizUiAvaloniaCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        // singleton: stateless except for DialogOwner, which the label editor host sets to
        // its window so IStorageProvider dialogs have a parent.
        serviceRegistry.RegisterSingleton<AvaloniaFileDialogService>();
        serviceRegistry.Register<IFileDialogService>(
            factory => factory.GetInstance<AvaloniaFileDialogService>());

        // service name must exactly match IViewFactory.GetLabelEditorView()
        serviceRegistry.Register<ILabelEditorView>(
            factory => new AvaloniaLabelEditorView(factory.GetInstance<AvaloniaFileDialogService>()),
            "LabelEditorView");

        // new-ui plan step 6, Part C: the Avalonia progress popup (a separate top-level
        // Avalonia window). Name must match IViewFactory.GetProgressBarView().
        serviceRegistry.Register<IProgressView, AvaloniaProgressView>("ProgressBarView");
    }
}
