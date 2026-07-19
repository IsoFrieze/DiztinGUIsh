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

        // pull in winforms-specific UI stuff:
        serviceRegistry.RegisterFrom<DizUiWinformsCompositionRoot>();

        // pull in OUR stuff, which is winforms-specific
        serviceRegistry.RegisterFrom<DizAppWinformsCompositionRoot>();

        // new-ui plan step 5, the runtime backend switch (see LabelEditorBackend docs; set
        // DIZ_LABEL_EDITOR=avalonia to flip it): registered LAST so its "LabelEditorView"
        // + IFileDialogService registrations override the WinForms ones above. Everything
        // else in the app stays WinForms.
        if (labelEditorBackend == LabelEditorBackendKind.Avalonia)
            serviceRegistry.RegisterFrom<DizUiAvaloniaCompositionRoot>();
    }
}