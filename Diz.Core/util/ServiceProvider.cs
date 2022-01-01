using System;
using LightInject;

namespace Diz.Core.util
{
    // TODO: this is currently horribly broken or stupid.
    // we're falling into the Service Locator anti-pattern with this,
    // and the singleton is ok for the app but breaks unit tests.
    // get rid of this and create one instance at startup, store
    // in the app main class only.
    
    public static class Service
    {
        public static ServiceContainer Container
        {
            get
            {
                _serviceContainerInst ??= _Recreate();
                return _serviceContainerInst.Value;
            }
        }

        public static void Recreate()
        {
            _serviceContainerInst = _Recreate();
        }

        private static Lazy<ServiceContainer> _serviceContainerInst = _Recreate();
        private static Lazy<ServiceContainer> _Recreate()
        {
            Shutdown();
            return new Lazy<ServiceContainer>(CreateServiceContainer);
        }

        private static void Shutdown()
        {
            // not really sure this all works but....
            if (!_serviceContainerInst.IsValueCreated) 
                return;
            
            _serviceContainerInst.Value.Dispose();
        }

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