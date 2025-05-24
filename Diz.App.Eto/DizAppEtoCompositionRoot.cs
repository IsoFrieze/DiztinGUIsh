using Diz.App.Common;
using Diz.Controllers.interfaces;
using Diz.Ui.Eto;
using JetBrains.Annotations;
using LightInject;

namespace Diz.App.Eto;

[UsedImplicitly] public class DizAppEtoCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.RegisterFrom<DizAppCommonCompositionRoot>();
        
        serviceRegistry.Register<IDizApp, DizEtoApp>();
        serviceRegistry.Register<ICommonGui, EtoCommonGui>();
    }
}