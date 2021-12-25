using System;
using LightInject;

namespace Diz.Core.util
{
    public static class Service
    {
        public static ServiceContainer Container => _serviceContainerInst.Value;
        
        public static void Recreate() => 
            _serviceContainerInst = _Recreate();
        
        
        private static Lazy<ServiceContainer> _serviceContainerInst = _Recreate();
        private static Lazy<ServiceContainer> _Recreate() => new (CreateServiceContainer);
        private static ServiceContainer CreateServiceContainer()
        {
            return new ServiceContainer(
                new ContainerOptions
                {
                    EnablePropertyInjection = false
                });
        }
    }
}