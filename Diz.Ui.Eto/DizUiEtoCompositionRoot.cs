using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Ui.Eto;

[UsedImplicitly] public class DizUiEtoCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        // TODO: register Eto-specific versions of this stuff
        // serviceRegistry.Register<IMainGridWindowView, PlaceholderForm>("MainGridWindowView");
        // serviceRegistry.Register<IFormViewer, PlaceholderForm>("AboutView");
        // serviceRegistry.Register<IImportRomDialogView, PlaceholderForm>("ImportRomView");
        // serviceRegistry.Register<IProgressView, PlaceholderForm>("ProgressBarView");
        // serviceRegistry.Register<ILogCreatorSettingsEditorView, PlaceholderForm>("ExportDisassemblyView");
        // serviceRegistry.Register<ILabelEditorView, PlaceholderForm>("LabelEditorView");

        // TODO: need one of these
        // serviceRegistry.RegisterSingleton<IDizAppSettings, DizWinformsAppSettingsProvider>();
    }
}