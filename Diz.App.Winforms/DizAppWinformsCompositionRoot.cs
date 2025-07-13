using Diz.App.Common;
using Diz.Controllers.interfaces;
using Diz.Core.Interfaces;
using Diz.Ui.Winforms;
using Diz.Ui.Winforms.dialogs;
using JetBrains.Annotations;
using LightInject;

namespace Diz.App.Winforms;

[UsedImplicitly] public class DizAppWinformsCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.RegisterFrom<DizAppCommonCompositionRoot>();
        
        serviceRegistry.Register<IDizApp, DizWinformsApp>();
        serviceRegistry.Register<ICommonGui, WinFormsCommonGui>();
        serviceRegistry.Register<IAppVersionInfo, AppVersionInfo>();
    }
}