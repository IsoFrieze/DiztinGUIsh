using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Diz.Core.services;
using Diz.Core.util;
using LightInject;

namespace Diz.Test.Utils;

// based on sample code from https://github.com/seesharper/LightInject under the "Unit Testing" section
// inject services into any private fields on derived classes
public class ContainerFixture : IDisposable
{
    [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
    public ContainerFixture()
    {
        var container = CreateContainer();
        
        Configure(container);
        RegisterServices(container);
        
        ServiceFactory = container.BeginScope();
        InjectPrivateFields();
    }

    internal virtual void RegisterServices(IServiceContainer container)
    {
        DizCoreServicesDllRegistration.RegisterServicesInDizDlls(container);
    }

    private void InjectPrivateFields()
    {
        var privateInstanceFields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var privateInstanceField in privateInstanceFields)
        {
            privateInstanceField.SetValue(this, GetInstance(ServiceFactory, privateInstanceField));
        }
    }

    internal Scope ServiceFactory { get; }

    public void Dispose() => ServiceFactory.Dispose();

    public TService GetInstance<TService>(string name = "")
        => ServiceFactory.GetInstance<TService>(name);

    private object GetInstance(IServiceFactory factory, FieldInfo field)
        => ServiceFactory.TryGetInstance(field.FieldType) ?? ServiceFactory.GetInstance(field.FieldType, field.Name);

    internal virtual IServiceContainer CreateContainer() => 
        DizServiceProvider.CreateServiceContainer();

    internal virtual void Configure(IServiceRegistry serviceRegistry) {}
}