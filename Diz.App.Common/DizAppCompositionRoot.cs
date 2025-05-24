using Diz.Controllers.interfaces;
using Diz.Controllers.services;
using Diz.Core.services;
using Diz.Cpu._65816;
using Diz.Import;
using Diz.LogWriter.services;
using JetBrains.Annotations;
using LightInject;

namespace Diz.App.Common;

[UsedImplicitly] public class DizAppCommonCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        // no platform-specific stuff in here.
        // i.e. no winforms/QT/etc.
        
        // the (string) names of the views above will be mapped to the method names of the interface below.
        // i.e. calling IViewFactory.GetLabelEditorView() will look for somthing named "LabelEditorView"
        serviceRegistry.EnableAutoFactories();
        
        serviceRegistry.RegisterFrom<DizCoreServicesCompositionRoot>();
        serviceRegistry.RegisterFrom<DizControllersCompositionRoot>();
        serviceRegistry.RegisterFrom<DizCpu65816ServiceRoot>();
        serviceRegistry.RegisterFrom<DizImportServiceRegistration>();
        serviceRegistry.RegisterFrom<LogWriterServiceRegistration>();
        
        serviceRegistry.RegisterAutoFactory<IViewFactory>();
    }
}