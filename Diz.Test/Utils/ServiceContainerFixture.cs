using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Diz.Core.services;
using Diz.Core.util;
using LightInject;
using Xunit.Abstractions;

namespace Diz.Test.Utils;

// if tagged with this, inject this field
// must be a private field on a class derived from ContainerFixture
[AttributeUsage(AttributeTargets.Field)]
public class Inject : Attribute
{
    
}

// based on sample code from https://github.com/seesharper/LightInject under the "Unit Testing" section
// inject services into any private fields on derived classes
public class ContainerFixture : IDisposable
{
    private readonly bool injectFieldsOnlyIfNull;
    private readonly bool injectOnlyTaggedFields;

    [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
    public ContainerFixture(bool injectFieldsOnlyIfNull = true, bool injectOnlyTaggedFields = true)
    {
        this.injectFieldsOnlyIfNull = injectFieldsOnlyIfNull;
        this.injectOnlyTaggedFields = injectOnlyTaggedFields;
        var container = ConfigureAndRegisterServiceContainer();
        ServiceFactory = container.BeginScope();
        InjectPrivateFields();
    }

    public virtual IServiceContainer ConfigureAndRegisterServiceContainer()
    {
        var container = CreateContainer();
        Configure(container);
        RegisterServices(container);
        return container;
    }

    private static IServiceContainer RegisterServices(IServiceContainer container)
    {
        DizCoreServicesDllRegistration.RegisterServicesInDizDlls(container);
        return container;
    }

    private void InjectPrivateFields()
    {
        var privateInstanceFields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var privateInstanceField in privateInstanceFields)
        {
            if (injectFieldsOnlyIfNull && privateInstanceField.GetValue(this) != null)
                continue;
            
            if (injectOnlyTaggedFields && !Attribute.IsDefined(privateInstanceField, typeof(Inject)))
                continue;
            
            privateInstanceField.SetValue(this, GetInstance(ServiceFactory, privateInstanceField));
        }
    }

    internal Scope ServiceFactory { get; }

    public void Dispose() => ServiceFactory.Dispose();

    public TService GetInstance<TService>(string name = "")
        => ServiceFactory.GetInstance<TService>(name);

    private object GetInstance(IServiceFactory factory, FieldInfo field)
    {
        // skip this type of interface so Xunit can populate it:
        if (field.FieldType.IsAssignableTo(typeof(ITestOutputHelper)))
            return null;

        return ServiceFactory.TryGetInstance(field.FieldType) ??
               ServiceFactory.GetInstance(field.FieldType, field.Name);
    }

    internal virtual IServiceContainer CreateContainer() => 
        CreateServiceContainer();

    public static IServiceContainer CreateServiceContainer() => 
        DizServiceProvider.CreateServiceContainer();

    protected virtual void Configure(IServiceRegistry serviceRegistry) {}
    
    protected static IServiceContainer CreateAndRegisterServiceContainer() => 
        RegisterServices(CreateServiceContainer());
}