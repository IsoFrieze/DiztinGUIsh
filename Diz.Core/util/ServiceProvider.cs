using LightInject;

namespace Diz.Core.util;

public static class DizServiceProvider
{
    // Thou shalt not create a global instance of this class, for verily thine unit tests.....
    // 
    // Don't cache this. Use IServiceContainer first thing at app startup to register services.
    // Then cast it down only to IServiceFactory and don't register anything after the first class is used.
    public static IServiceContainer CreateServiceContainer()
    {
        var containerOptions = new ContainerOptions
        {
            EnablePropertyInjection = false,
        };
        return new ServiceContainer(containerOptions);
    }   
}