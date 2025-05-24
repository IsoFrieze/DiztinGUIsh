using Diz.Core.services;
using Diz.Core.util;
using LightInject;

namespace Diz.App.Winforms;

public static class DizAppServices
{
    public static IServiceFactory CreateServiceFactoryAndRegisterTypes()
    {
        var serviceProvider = DizServiceProvider.CreateServiceContainer();
        RegisterDizUiServices(serviceProvider);
        
        return serviceProvider;
    }

    public static void RegisterDizUiServices(IServiceRegistry serviceRegistry)
    {
        // register services in any Diz*dll's present
        DizCoreServicesDllRegistration.RegisterServicesInDizDlls(serviceRegistry);

        // alternatively, we can be explicit like below, no DLL scanning required
        // serviceProvider.RegisterFrom<DizCoreServicesCompositionRoot>();
        // serviceProvider.RegisterFrom<DizCpu65816ServiceRoot>();
        // serviceProvider.RegisterFrom<DizControllersCompositionRoot>();
        // serviceProvider.RegisterFrom<DizWinformsCompositionRoot>();

        // scan ourselves last
        serviceRegistry.RegisterFrom<DizUiWinformsCompositionRoot>();
    }
}