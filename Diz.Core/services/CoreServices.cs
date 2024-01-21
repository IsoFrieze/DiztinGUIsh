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
        serviceRegistry.Register<IProjectFileManager, ProjectFileManager>();

        serviceRegistry.Register<IXmlSerializerFactory, XmlSerializerFactory>();
        
        serviceRegistry.Register<IProjectXmlSerializer>(factory => new ProjectXmlSerializer(
            xmlSerializerFactory: factory.GetInstance<IXmlSerializerFactory>(),
            migrationRunner: factory.GetInstance<IMigrationRunner>()
        ));

        serviceRegistry.Register<IDataFactory, DataFactory>();

        // default one that reads 1:1 from a file
        serviceRegistry.Register<IFileByteProvider, FileByteProviderSingleFile>();
        
        serviceRegistry.Register<Func<string, IFileByteProvider>>(c => type =>
        {
            return type switch
            {
                "Single" => c.GetInstance<FileByteProviderSingleFile>(),
                "Multiple" => c.GetInstance<FileByteProviderMultipleFiles>(),
                _ => throw new InvalidOperationException($"No file bytes type handler found for type: {type}")
            };
        });
        
        serviceRegistry.Register<FileByteProviderSingleFile>();
        serviceRegistry.Register<FileByteProviderMultipleFiles>();

        serviceRegistry.Register<IDataFactory, XmlSerializerFactory.SnesDataInterceptor>((factory, dataFactory) => 
            new XmlSerializerFactory.SnesDataInterceptor(dataFactory));

        serviceRegistry.RegisterFallback((type, serviceType) => 
            type == typeof(IReadFromFileBytes), 
            request => new ReadFromFileBytes());
        
        serviceRegistry.Register<ILinkedRomBytesProvider, LinkedRomBytesFileSearchProvider>();
    }
}