using System;
using System.Linq;
using System.Reflection;
using Diz.Core.export;
using LightInject;

namespace Diz.Core.util
{
    public static class ServiceProvider
    {
        private static ServiceContainer _serviceContainerInst; 
        public static ServiceContainer ServiceContainer => 
            _serviceContainerInst ??= CreateServiceContainer();

        private static ServiceContainer CreateServiceContainer()
        {
            ServiceContainer serviceContainer = new();
            return serviceContainer;
        }
        
        // THIS WORKS
        public static void Register(Assembly assembly, Type implementingInterface)
        {
            ServiceContainer.RegisterAssembly(assembly, 
                (serviceType, implementingType) => 
                    !serviceType.IsClass && implementingType.GetInterfaces().Contains(implementingInterface));   
        }
    }
}