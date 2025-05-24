using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Ui.Eto;

[UsedImplicitly] public class DizEtoCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        // note: service names (the strings) here must exactly match IViewFactory method names
        // serviceRegistry.Register<IMainGridWindowView, MainWindow>("MainGridWindowView");
        // serviceRegistry.Register<IFormViewer, About>("AboutView");
        // serviceRegistry.Register<IImportRomDialogView, ImportRomDialog>("ImportRomView");
        // serviceRegistry.Register<IProgressView, ProgressDialog>("ProgressBarView");
        // serviceRegistry.Register<ILogCreatorSettingsEditorView, LogCreatorSettingsEditorForm>("ExportDisassemblyView");
        // serviceRegistry.Register<ILabelEditorView, AliasList>("LabelEditorView");

        serviceRegistry.RegisterSingleton<IDizAppSettings, DizWinformsAppSettingsProvider>();
    }
}