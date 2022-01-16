using System;
using System.IO;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Core.model.snes;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using ExtendedXmlSerializer.ExtensionModel.Instances;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Core.services;

public static class DizCoreServicesDllRegistration
{
    public static void RegisterServicesInDizDlls(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.RegisterAssembly("Diz*.dll");
    }
}

[UsedImplicitly]
public class DizCoreServicesCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.Register<IFilesystemService, FilesystemService>();

        serviceRegistry.Register<IData, Data>();

        serviceRegistry.Register<IMigrationRunner, MigrationRunner>();

        serviceRegistry.Register<IProjectFactory, ProjectFactory>();
        serviceRegistry.Register<IProjectImportSettingsFactory, ProjectImportSettingsFactory>();
        serviceRegistry.Register<IProjectImporter, ProjectImporter>();

        serviceRegistry.Register<IXmlSerializerFactory, XmlSerializerFactory>();
        serviceRegistry.Register<IProjectXmlSerializer, ProjectXmlSerializer>();

        serviceRegistry.Register<IDataFactory, DataFactory>();

        serviceRegistry.Register<IFileByteProvider, FileByteProvider>();

        serviceRegistry.Register<IDataFactory, XmlSerializerFactory.SnesDataInterceptor>((factory, dataFactory) => 
            new XmlSerializerFactory.SnesDataInterceptor(dataFactory));

        serviceRegistry.Register<ILinkedRomBytesProvider, LinkedRomBytesFileSearchProvider>();
    }
}