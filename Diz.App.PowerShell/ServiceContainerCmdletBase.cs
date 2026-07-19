using System.Management.Automation;
using Diz.Core.util;
using LightInject;

namespace Diz.PowerShell;

public abstract class ServiceContainerCmdletBase : PSCmdlet
{
    protected IServiceContainer? ServiceContainer { get; private set; }

    protected override void BeginProcessing()
    {
        if (ServiceContainer != null)
            return;

        ServiceContainer = DizServiceProvider.CreateServiceContainer();
        ServiceContainer.RegisterFrom<DizPowerShellCompositionRoot>();

        // logger must be bound to this live cmdlet instance so Info/Warn/Error land on
        // the PowerShell output/warning/error streams instead of vanishing.
        ServiceContainer.RegisterInstance<IDizLogger>(new CmdletDizLogger(this));
    }

    protected override void EndProcessing()
    {
        ServiceContainer?.Dispose();
        ServiceContainer = null;
    }
}
