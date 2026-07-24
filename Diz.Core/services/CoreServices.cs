using System;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Core.model.snes;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
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

        serviceRegistry.Register<IProjectFileUserPrefs, ProjectFileUserPrefs>();
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

        // machine-global fallback for locating a project's ROM when its (gitignored) user-prefs
        // don't point at one - e.g. a fresh checkout or a sibling worktree. Factory registration so
        // the default-path constructor is used (the path-injectable overload is for tests only).
        //
        // Wired as an OPTIONAL constructor dependency: consumers get null when it isn't registered,
        // which cleanly disables the registry fallback + auto-populate. To turn the feature OFF,
        // comment out the Register line below - the RegisterConstructorDependency then resolves it
        // to null. (LightInject does NOT fall back to a parameter's default value on its own; it
        // needs this explicit optional wiring, or it throws on the unregistered dependency.)
        serviceRegistry.RegisterConstructorDependency<IGlobalRomRegistry>(
            (factory, _) => factory.TryGetInstance<IGlobalRomRegistry>());
        serviceRegistry.Register<IGlobalRomRegistry>(_ => new GlobalRomRegistry());
    }
}