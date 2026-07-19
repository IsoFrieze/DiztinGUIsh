using Diz.Core;
using Diz.Core.services;
using Diz.Cpu._65816;
using Diz.LogWriter.services;
using LightInject;

namespace Diz.PowerShell;

public class DizPowerShellCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        // same non-UI stack the GUI apps register (see DizAppCommonCompositionRoot),
        // minus controllers/views: enough to open a project file and export assembly.
        serviceRegistry.RegisterFrom<DizCoreServicesCompositionRoot>();
        serviceRegistry.RegisterFrom<DizCpu65816ServiceRoot>();
        serviceRegistry.RegisterFrom<LogWriterServiceRegistration>();

        serviceRegistry.Register<IProjectFileAssemblyExporter, ProjectFileAssemblyExporter>();
        serviceRegistry.Register<IProjectFileOpener, ProjectFileReader>();

        // note: IDizLogger is NOT registered here -- each cmdlet registers a live
        // instance bound to itself so output lands on the PowerShell streams.
    }
}
