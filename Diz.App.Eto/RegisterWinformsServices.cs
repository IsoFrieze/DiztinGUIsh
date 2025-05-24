using Diz.App.Common;
using Diz.Core.util;
using Diz.Ui.Eto;
using LightInject;

namespace Diz.App.Eto;

public static class DizEtoRegisterServices
{
    public static IServiceFactory CreateServiceFactoryAndRegisterTypes()
    {
        var serviceProvider = DizServiceProvider.CreateServiceContainer();
        RegisterDizUiServices(serviceProvider);
        
        return serviceProvider;
    }

    public static void RegisterDizUiServices(IServiceRegistry serviceRegistry)
    {
        // option #1: we can simply register services in any Diz*dll's that are found in a scan.
        // this is easy but we have less control
        // DizCoreServicesDllRegistration.RegisterServicesInDizDlls(serviceRegistry);

        // option #2: register everything by hand (this is what we'll do).
        
        // pull in all common stuff (platform-independent)
        serviceRegistry.RegisterFrom<DizAppCommonCompositionRoot>();
        
        // pull in winforms-specific UI stuff:
        serviceRegistry.RegisterFrom<DizUiEtoCompositionRoot>();
        
        // finally, pull in OUR stuff, which is winforms-specific
        serviceRegistry.RegisterFrom<DizAppEtoCompositionRoot>();
    }
}