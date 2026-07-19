using Diz.App.Common;
using Diz.Core.util;
using Diz.Ui.Avalonia;
using Diz.Ui.Winforms;
using LightInject;

namespace Diz.App.Winforms;

public static class DizWinformsRegisterServices
{
    public static IServiceFactory CreateServiceFactoryAndRegisterTypes()
    {
        var serviceProvider = DizServiceProvider.CreateServiceContainer();
        RegisterDizUiServices(serviceProvider, LabelEditorBackend.FromEnvironment());

        return serviceProvider;
    }

    public static void RegisterDizUiServices(
        IServiceRegistry serviceRegistry,
        LabelEditorBackendKind labelEditorBackend = LabelEditorBackendKind.WinForms)
    {
        // option #1: we can simply register services in any Diz*dll's that are found in a scan.
        // this is easy but we have less control
        // DizCoreServicesDllRegistration.RegisterServicesInDizDlls(serviceRegistry);

        // option #2: register everything by hand (this is what we'll do).

        // pull in all common stuff (platform-independent)
        serviceRegistry.RegisterFrom<DizAppCommonCompositionRoot>();

        // pull in winforms-specific UI stuff (the views that are NOT backend-selectable):
        serviceRegistry.RegisterFrom<DizUiWinformsCompositionRoot>();

        // pull in OUR stuff, which is winforms-specific
        serviceRegistry.RegisterFrom<DizAppWinformsCompositionRoot>();

        // new-ui plan step 6: EXPLICIT backend branch (Dom's directive), replacing the old
        // step-5 last-registration-wins ordering trick. Exactly ONE of these roots is ever
        // registered, so the label-editor backend selection has no dependency on registration
        // order. Each root registers the same three named seams -- LabelEditorView,
        // ProgressBarView, IFileDialogService -- for its toolkit. Everything not listed here
        // stays WinForms (registered unconditionally above). Set DIZ_LABEL_EDITOR=avalonia to
        // pick the Avalonia backend (see LabelEditorBackend docs).
        if (labelEditorBackend == LabelEditorBackendKind.Avalonia)
            serviceRegistry.RegisterFrom<DizUiAvaloniaCompositionRoot>();
        else
            serviceRegistry.RegisterFrom<DizUiWinformsBackendCompositionRoot>();
    }
}