using Diz.Controllers.interfaces;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Ui.Tui;

/// <summary>
/// The TUI LABEL-EDITOR BACKEND root. UNLIKE the WinForms/Avalonia backend roots, the TUI backend
/// only provides ONE of the three backend-selectable seams: the label editor. The progress popup
/// and file dialogs stay WinForms, so this root registers ONLY <see cref="TuiLabelEditorView"/> as
/// "LabelEditorView"; the app's explicit tui branch (DizWinformsRegisterServices) registers the two
/// WinForms services alongside it -- explicitly, never by last-registration-wins ordering.
///
/// Selected by DIZ_LABEL_EDITOR=tui. Proven by test: LabelEditorBackendSwitchTests in
/// Diz.App.Winforms.Test.
/// </summary>
[UsedImplicitly]
public class DizUiTuiCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        // service name must exactly match IViewFactory.GetLabelEditorView().
        serviceRegistry.Register<ILabelEditorView, TuiLabelEditorView>("LabelEditorView");
    }
}
