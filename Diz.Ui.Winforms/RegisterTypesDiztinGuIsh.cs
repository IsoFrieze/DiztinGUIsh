using Diz.Controllers.interfaces;
using Diz.Ui.Winforms.dialogs;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Ui.Winforms;

[UsedImplicitly] public class DizWinformsCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.Register<IFormViewer, About>("About");
    }
}