// using Diz.Controllers.controllers;

using Diz.Controllers.controllers;
using Diz.Controllers.importers;
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
        serviceRegistry.Register<ILargeFilesReaderController, LargeFilesReader>("LargeFileReaderProgressController");

        // the SNES importer. Transient on purpose: it drives a settings builder holding one
        // analysed ROM, so each import must start from a fresh one.
        serviceRegistry.Register<SnesRomImporter>();
        
        serviceRegistry.EnableAutoFactories();
        serviceRegistry.RegisterAutoFactory<IControllerFactory>();

        serviceRegistry.Register<IDizDocument, DizDocument>();

    }
}