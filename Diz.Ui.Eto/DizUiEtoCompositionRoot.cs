using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Ui.Eto.ui;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Ui.Eto;

[UsedImplicitly] public class DizUiEtoCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        // TODO: register Eto-specific versions of this stuff
        serviceRegistry.Register<IMainGridWindowView, EtoMainGridForm>("MainGridWindowView");
        serviceRegistry.Register<IProgressView, EtoProgressForm>("ProgressBarView");
        // serviceRegistry.Register<IFormViewer, PlaceholderForm>("AboutView");
        // serviceRegistry.Register<ISnesImportRomView, PlaceholderForm>("SnesImportRomView");
        // serviceRegistry.Register<IExportSettingsView, PlaceholderForm>("ExportSettingsView");
        // serviceRegistry.Register<ILabelEditorView, PlaceholderForm>("LabelEditorView");
        
        serviceRegistry.RegisterSingleton<IDizAppSettings, DizEtoAppSettingsProvider>();
    }
}