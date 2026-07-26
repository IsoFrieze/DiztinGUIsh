// using Diz.Controllers.controllers;

using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Controllers.util;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Controllers.services;

[UsedImplicitly]
public class DizControllersCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.Register<IProjectController, ProjectController>("ProjectController");
        serviceRegistry.Register<ILogCreatorSettingsEditorController, LogCreatorSettingsEditorController>("AssemblyExporterSettingsController");
        serviceRegistry.Register<IImportRomDialogController, ImportRomDialogController>("ImportRomDialogController");
        serviceRegistry.Register<ILargeFilesReaderController, LargeFilesReader>("LargeFileReaderProgressController");
        
        serviceRegistry.EnableAutoFactories();
        serviceRegistry.RegisterAutoFactory<IControllerFactory>();

        serviceRegistry.Register<IDizDocument, DizDocument>();

    }
}