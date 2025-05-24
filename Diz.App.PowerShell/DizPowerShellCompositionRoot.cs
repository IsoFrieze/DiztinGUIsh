using Diz.Core;
using LightInject;

namespace Diz.PowerShell;

public class DizPowerShellCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        // TODO serviceRegistry.Register<IPowershellLogger>()

        serviceRegistry.Register<IDizLogger>();
        serviceRegistry.Register<IProjectFileAssemblyExporter, ProjectFileAssemblyExporter>();
        serviceRegistry.Register<IProjectFileOpener, ProjectFileReader>();
    }
}