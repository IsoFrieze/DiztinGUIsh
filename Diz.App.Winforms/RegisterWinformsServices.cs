using Diz.Core.services;
using Diz.Core.util;
using LightInject;

namespace Diz.App.Winforms;

public static class DizWinformsRegisterServices
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
        // this is easy:
        DizCoreServicesDllRegistration.RegisterServicesInDizDlls(serviceRegistry);

        // option #2: register everything by hand
        // alternatively, we can be explicit like below, no DLL scanning required
        // serviceProvider.RegisterFrom<DizCoreServicesCompositionRoot>();
        // serviceProvider.RegisterFrom<DizCpu65816ServiceRoot>();
        // serviceProvider.RegisterFrom<DizControllersCompositionRoot>();
        // serviceProvider.RegisterFrom<DizWinformsCompositionRoot>();

        // scan ourselves last
        serviceRegistry.RegisterFrom<DizUiWinformsCompositionRoot>();
    }
}