using Diz.Controllers.interfaces;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Ui.Avalonia;

/// <summary>
/// Registers the Avalonia UI backend (new-ui plan step 5). Mirrors
/// DizUiWinformsCompositionRoot's shape: view registrations are named with the exact
/// IViewFactory method-name string, and the toolkit's IFileDialogService is a singleton.
///
/// Today this backend supplies ONE view -- the label editor. The app decides at composition
/// time which backend's "LabelEditorView" wins: Diz.App.Winforms registers this root AFTER
/// the WinForms root when the DIZ_LABEL_EDITOR env var selects avalonia, and LightInject's
/// last-registration-wins override does the rest (proven by test, not assumed:
/// LabelEditorBackendSwitchTests in Diz.Ui.Winforms.Test).
///
/// NOTE: when this root is loaded, its IFileDialogService also overrides the WinForms one.
/// That is correct today because the label editor view is the seam's only consumer; if a
/// WinForms-hosted view ever starts consuming IFileDialogService, the two backends' file
/// dialog services will need named registrations instead.
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
    }
}
