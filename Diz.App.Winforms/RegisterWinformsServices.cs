using Diz.App.Common;
using Diz.Controllers.interfaces;
using Diz.Core.util;
using Diz.Ui.Avalonia;
using Diz.Ui.Tui;
using Diz.Ui.Winforms;
using Diz.Ui.Winforms.dialogs;
using Diz.Ui.Winforms.util;
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
        // order. Each root registers the same named seams -- LabelEditorView, RegionEditorView,
        // MarkManyView, GotoView, HarshAutoStepView, SnesImportRomView, MisalignmentCheckerView,
        // InOutPointCheckerView, NavigationHistoryView, ProgressBarView, IFileDialogService -- for
        // its toolkit.
        // Everything not listed here
        // stays WinForms (registered unconditionally above). Set DIZ_LABEL_EDITOR=avalonia to
        // pick the Avalonia backend (see LabelEditorBackend docs).
        if (labelEditorBackend == LabelEditorBackendKind.Avalonia)
        {
            serviceRegistry.RegisterFrom<DizUiAvaloniaCompositionRoot>();

            // TEMPORARY: the export-settings window has no Avalonia implementation yet, so this
            // backend borrows the WinForms one. Delete this line the moment the Avalonia window
            // is registered in DizUiAvaloniaCompositionRoot -- registering both would make the
            // backend selection depend on registration order again.
            serviceRegistry.Register<IExportSettingsView, WinformsExportSettingsView>("ExportSettingsView");
        }
        else if (labelEditorBackend == LabelEditorBackendKind.Tui)
        {
            // TUI backend (DIZ_LABEL_EDITOR=tui): ONLY the label editor is TUI. Unlike the
            // Avalonia/WinForms roots (which supply every seam for their toolkit), the TUI
            // root supplies just LabelEditorView; the region editor, navigation history, mark-many window, goto
            // window, harsh-auto-step window, misaligned-flags window, in/out-point rescan
            // confirmation, new-project import window, progress popup and file dialogs stay WinForms and are
            // registered EXPLICITLY here. We do NOT
            // register DizUiWinformsBackendCompositionRoot for those, because it would also
            // register a WinForms LabelEditorView and reintroduce the last-registration-wins
            // ordering this branch exists to avoid.
            serviceRegistry.RegisterFrom<DizUiTuiCompositionRoot>();
            serviceRegistry.Register<IProgressView, ProgressDialog>("ProgressBarView");
            // the TUI has no region screen, so the region editor is the WinForms one here.
            DizUiWinformsBackendCompositionRoot.RegisterRegionEditorView(serviceRegistry);
            // ...nor a navigation-history screen, same fallback.
            DizUiWinformsBackendCompositionRoot.RegisterNavigationHistoryView(serviceRegistry);
            serviceRegistry.Register<IMarkManyView, WinformsMarkManyView>("MarkManyView");
            serviceRegistry.Register<IGotoView, WinformsGotoView>("GotoView");
            serviceRegistry.Register<IHarshAutoStepView, WinformsHarshAutoStepView>("HarshAutoStepView");
            serviceRegistry.Register<ISnesImportRomView, WinformsSnesImportRomView>("SnesImportRomView");
            serviceRegistry.Register<IMisalignmentCheckerView, WinformsMisalignmentCheckerView>("MisalignmentCheckerView");
            serviceRegistry.Register<IInOutPointCheckerView, WinformsInOutPointCheckerView>("InOutPointCheckerView");
            serviceRegistry.Register<IExportSettingsView, WinformsExportSettingsView>("ExportSettingsView");
            serviceRegistry.RegisterSingleton<IFileDialogService, WinformsFileDialogService>();
        }
        else
        {
            serviceRegistry.RegisterFrom<DizUiWinformsBackendCompositionRoot>();
        }
    }
}