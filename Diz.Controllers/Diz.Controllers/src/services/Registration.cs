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
        serviceRegistry.Register<ILargeFilesReaderController, LargeFilesReader>("LargeFileReaderProgressController");

        // the SNES importer, registered under the platform seam rather than by its own type: this
        // is the list the registry is handed, so a second console becomes reachable by adding a
        // line here and nothing else. Transient on purpose: an importer drives a settings builder
        // holding one analysed ROM, so each import must start from a fresh one.
        serviceRegistry.Register<IRomImporter, SnesRomImporter>("SnesRomImporter");

        // transient for the same reason -- resolving a registry resolves fresh importers with it.
        serviceRegistry.Register<RomImporterRegistry>();

        serviceRegistry.EnableAutoFactories();
        serviceRegistry.RegisterAutoFactory<IControllerFactory>();

        serviceRegistry.Register<IDizDocument, DizDocument>();

    }
}