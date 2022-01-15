using System;
using System.Collections.Generic;
using System.Linq;
using Diz.Controllers.interfaces;
using Diz.Test.Utils;
using ExtendedXmlSerializer.Configuration;
using FluentAssertions;
using LightInject;
using Moq;
using Xunit;

namespace Diz.Test.Tests.RomInterfaceTests;

/// <summary>
/// Look through all registered interfaces in the service container, try and instantiate
/// each one and make sure nothing breaks at runtime. Failed tests typically indicate you need
/// to register class dependencies in the CompositionRoot classes.
/// </summary>
public class TestServiceInterfaces : ContainerFixture
{
    public static IEnumerable<object[]> Interfaces =>
        CreateAndRegisterServiceContainer().AvailableServices
            .Select(x => x.ServiceType)
            .Select(type => new[] { type });

    protected override void Configure(IServiceRegistry serviceRegistry)
    {
        MockGuiInterfaces(serviceRegistry);
    }

    private static void MockGuiInterfaces(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.Register(_ => new Mock<ICommonGui>().Object);
        serviceRegistry.Register(_ => new Mock<IImportRomDialogView>().Object);
    }

    [Theory(Skip = "Useful more for debugging registrations, less so as a comrehensive unit test"), MemberData(nameof(Interfaces))]
    public void CreateInstance(Type interfaceToTest)
    {
        ServiceFactory.GetInstance(interfaceToTest).Should().NotBeNull();
    }
}