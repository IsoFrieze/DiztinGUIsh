using Diz.Controllers.controllers;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Controllers.interfaces;

// TODO: can be replaced with "auto factory" in LightInject

[UsedImplicitly]
public class ControllerFactory : IControllerFactory
{
    private readonly IServiceFactory serviceFactory;
        
    public ControllerFactory(IServiceFactory serviceFactory) => 
        this.serviceFactory = serviceFactory;
    
    public IController Get(string name) => 
        serviceFactory.GetInstance<IController>(name);

    public ILogCreatorSettingsEditorController GetLogCreatorSettingsEditorController() => 
        serviceFactory.GetInstance<ILogCreatorSettingsEditorController>();

    public IImportRomDialogController GetImportRomDialogController() =>
        serviceFactory.GetInstance<IImportRomDialogController>();
}