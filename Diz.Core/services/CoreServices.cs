using Diz.Core.util;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Core.services;

[UsedImplicitly]
public class DizCoreServicesCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.Register<IFilesystemService, FilesystemService>();
    }
}