using System.Linq;
using Diz.Controllers.services;
using Diz.Core.services;
using Diz.Core.util;
using Diz.Cpu._65816;
using FluentAssertions;

namespace Diz.Test.Utils;

public static class AppServicesForTests
{
    public static void RegisterNormalAppServices()
    {
        Service.Recreate();
        
        Service.Container.GetAllInstances(typeof(object)).Count().Should().Be(0);
        
        Service.Container.RegisterFrom<DizCoreServicesCompositionRoot>();
        Service.Container.RegisterFrom<DizCpu65816ServiceRoot>();
        Service.Container.RegisterFrom<DizControllersCompositionRoot>();
    }
}