using System;
using Diz.Core;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Test.Utils;
using FluentAssertions;
using LightInject;
using Xunit;

namespace Diz.Test.Tests.SerializationTests;

public class ProjectServicesTests : ContainerFixture
{
    [Inject] private readonly Func<ILinkedRomBytesProvider> createLinkedProvider = null!;
    
    [Fact]
    public void TestVariousServicesExist()
    {
        ServiceFactory.GetInstance<IReadFromFileBytes>();
        createLinkedProvider().Should().NotBeNull();
        
        ServiceFactory.GetAllInstances<IMigrationRunner>().Should().HaveCountGreaterOrEqualTo(1);
        ServiceFactory.GetInstance<IProjectFactory>();
        ServiceFactory.GetInstance<IProjectImportSettingsFactory>();
        ServiceFactory.GetInstance<IProjectImporter>();
        ServiceFactory.GetInstance<IProjectFileManager>();
        ServiceFactory.GetInstance<IXmlSerializerFactory>();
        ServiceFactory.GetInstance<IProjectXmlSerializer>();
        ServiceFactory.GetInstance<IDataFactory>();
        ServiceFactory.GetInstance<IFileByteProvider>();
    }
}