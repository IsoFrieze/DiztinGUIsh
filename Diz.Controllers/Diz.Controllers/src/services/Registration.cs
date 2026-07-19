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

        // sorry this is all a huge WIP mess, cleanup incoming soon.

        // serviceRegistry.Register<int, int, IReadOnlySnesRom, IMarkManyController>(
        //     (factory, offset, whichIndex, data) =>
        //     {
        //          // TODO: update this with updated controller from Diz 2.0 branch.
        //          // I think that means kill 'whichIndex', use the new format that doesn't rely on it.
        //         var view = factory.GetInstance<IMarkManyView>();
        //         var markManyController = new MarkManyController(offset, whichIndex, data, view);
        //         markManyController.MarkManyView.Controller = markManyController;
        //         return markManyController;
        //     });

    }
}