using System;
using System.Linq;
using LightInject;

namespace Diz.Core.util
{
    public static class Service
    {
        private static ServiceContainer _serviceContainerInst; 
        public static ServiceContainer Container => 
            _serviceContainerInst ??= CreateServiceContainer();

        private static ServiceContainer CreateServiceContainer()
        {
            ServiceContainer serviceContainer = new();
            return serviceContainer;
        }
        
        // dont use this anymore
        [Obsolete]
        public static void Register(Type klass, Type iface)
        {
            // this may not be the most safe implementation. ok for now.
            Container.RegisterAssembly(klass.Assembly, 
                (serviceType, implementingType) => 
                    !serviceType.IsClass && implementingType.GetInterfaces().Contains(iface));   
        }
    }
}