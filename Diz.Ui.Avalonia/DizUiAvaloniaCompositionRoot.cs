using Diz.Controllers.interfaces;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia LABEL-EDITOR BACKEND (new-ui plan step 5/6): exactly the
/// backend-selectable registrations (LabelEditorView, RegionEditorView, NavigationHistoryView,
/// MarkManyView, GotoView, HarshAutoStepView, SnesImportRomView,
/// MisalignmentCheckerView, InOutPointCheckerView, ProgressBarView, IFileDialogService), each
/// named with the exact IViewFactory method-name
/// string. The app registers EITHER this
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

        // the region editor window. Long-lived like the label editor: MainWindow resolves one in
        // its constructor and keeps it for the application's lifetime, and it hides rather than
        // closes. Still one per resolve, so two owners never share a window. Name must match
        // IViewFactory.GetRegionEditorView().
        serviceRegistry.Register<IRegionListView, AvaloniaRegionListView>("RegionEditorView");

        // the navigation-history window. Long-lived and hide-on-close like the region editor, and
        // one per resolve for the same reason -- each owner hands its view the ViewModel it owns,
        // so a shared instance would leave one main window displaying the other's history. Name
        // must match IViewFactory.GetNavigationHistoryView().
        serviceRegistry.Register<INavigationHistoryView, AvaloniaNavigationHistoryView>(
            "NavigationHistoryView");

        // new-ui plan step 6, Part C: the Avalonia progress popup (a separate top-level
        // Avalonia window). Name must match IViewFactory.GetProgressBarView().
        serviceRegistry.Register<IProgressView, AvaloniaProgressView>("ProgressBarView");

        // the mark-many window. A fresh instance per resolve: the view is created, used for one
        // edit, and discarded. Name must match IViewFactory.GetMarkManyView().
        serviceRegistry.Register<IMarkManyView, AvaloniaMarkManyView>("MarkManyView");

        // the goto window, same per-invocation lifetime. Name must match
        // IViewFactory.GetGotoView().
        serviceRegistry.Register<IGotoView, AvaloniaGotoView>("GotoView");

        // the harsh-auto-step window, same per-invocation lifetime. Name must match
        // IViewFactory.GetHarshAutoStepView().
        serviceRegistry.Register<IHarshAutoStepView, AvaloniaHarshAutoStepView>("HarshAutoStepView");

        // the misaligned-flags window, same per-invocation lifetime. Name must match
        // IViewFactory.GetMisalignmentCheckerView().
        serviceRegistry.Register<IMisalignmentCheckerView, AvaloniaMisalignmentCheckerView>("MisalignmentCheckerView");

        // the in/out-point rescan confirmation, same per-invocation lifetime. Name must match
        // IViewFactory.GetInOutPointCheckerView().
        serviceRegistry.Register<IInOutPointCheckerView, AvaloniaInOutPointCheckerView>("InOutPointCheckerView");

        // the new-project ROM import window, same per-invocation lifetime -- the importer resolves
        // a second one to re-show the same ViewModel when the user declines its confirmation
        // prompt. Name must match IViewFactory.GetSnesImportRomView().
        serviceRegistry.Register<ISnesImportRomView, AvaloniaSnesImportRomView>("SnesImportRomView");
    }
}
