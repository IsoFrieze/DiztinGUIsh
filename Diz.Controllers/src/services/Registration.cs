using System.Diagnostics;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Controllers.services
{
    [UsedImplicitly]
    public class DizControllersCompositionRoot : ICompositionRoot
    {
        public void Compose(IServiceRegistry serviceRegistry)
        {
            // TODO: might be able to make some of these register using
            // "open generics" to be more flexible.
            
            serviceRegistry.Register(
                typeof(IDataController), 
                typeof(RomByteDataBindingController<IGridRow<ByteEntry>>)
            );

            serviceRegistry.Register<IDataController, RomByteDataBindingGridController>();

            serviceRegistry.Register(
                typeof(IBytesGridDataController<,>),
                typeof(RomByteDataBindingController<>)
            );
            
            serviceRegistry.Register(
                typeof(IBytesGridDataController<IDataGridRow,ByteEntry>),
                typeof(RomByteDataBindingGridController)
            );
            
            serviceRegistry.Register<IStartFormController, StartFormController>();
            
            serviceRegistry.Register<int, int, IReadOnlySnesRom, IMarkManyController>(
                (factory, offset, whichIndex, data) =>
                {
                    var view = factory.GetInstance<IMarkManyView>();
                    var markManyController = new MarkManyController(offset, whichIndex, data, view);
                    markManyController.MarkManyView.Controller = markManyController;
                    return markManyController;
                });
            
            serviceRegistry.Register<IMainFormController, MainFormController>();
            
            serviceRegistry.Register<IProjectLoader, ProjectFileLoader>();
            serviceRegistry.Decorate(
                typeof(IProjectLoader), 
                typeof(ProjectLoaderWithSampleDataDecorator));

            serviceRegistry.Register<IProjectsManager, ProjectsManager>();
            
            serviceRegistry.RegisterSingleton<ISampleProjectLoader, ProjectsManager>("SampleProjectLoader");
        }
    }
}