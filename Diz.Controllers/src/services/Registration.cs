using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
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
            // TODO: might be able to make some of these implement more
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
            serviceRegistry.Register<IMarkManyController, MarkManyController>();
            serviceRegistry.Register<IMainFormController, MainFormController>();
        }
    }
}