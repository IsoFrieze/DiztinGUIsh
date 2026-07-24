using Diz.Core.util;
using FluentAssertions;
using LightInject;
using Xunit;

namespace Diz.Test.Tests.SerializationTests;

// Documents/guards the DI behaviour the optional IGlobalRomRegistry dependency relies on:
// LightInject does NOT use a constructor parameter's default value for an unregistered service (it
// throws). To make a dependency genuinely optional (null when unregistered), it must be wired with
// RegisterConstructorDependency + TryGetInstance - which is exactly how CoreServices registers
// IGlobalRomRegistry so the feature can be turned off by dropping its registration.
public class OptionalDependencyWiringTests
{
    private interface IFoo { }
    private class Foo : IFoo { }
    private class NeedsFoo
    {
        public readonly IFoo? Foo;
        public NeedsFoo(IFoo? foo = null) => Foo = foo;
    }

    [Fact]
    public void OptionalDependency_IsNull_WhenServiceUnregistered()
    {
        var c = DizServiceProvider.CreateServiceContainer();
        c.RegisterConstructorDependency<IFoo>((factory, _) => factory.TryGetInstance<IFoo>());
        c.Register<NeedsFoo>();

        c.GetInstance<NeedsFoo>().Foo.Should().BeNull();
    }

    [Fact]
    public void OptionalDependency_IsInjected_WhenServiceRegistered()
    {
        var c = DizServiceProvider.CreateServiceContainer();
        c.Register<IFoo, Foo>();
        c.RegisterConstructorDependency<IFoo>((factory, _) => factory.TryGetInstance<IFoo>());
        c.Register<NeedsFoo>();

        c.GetInstance<NeedsFoo>().Foo.Should().NotBeNull();
    }
}
