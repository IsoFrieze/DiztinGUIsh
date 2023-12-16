using Diz.Core;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Cpu._65816.import;
using JetBrains.Annotations;
using LightInject;

namespace Diz.Cpu._65816;

[UsedImplicitly]
public class DizCpu65816ServiceRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.Register<IAddRomDataCommand, AddRomDataCommand>();

        // when we create a IData registration, add a SNES API component to it
        serviceRegistry.Decorate<IDataFactory, DataAddSnesApiDecorator>();

        serviceRegistry.Register<ImportRomSettings, IProjectFactoryFromRomImportSettings>((factory, settings) =>
            new SnesProjectFactoryFromRomImportSettings(
                factory.GetInstance<IProjectFactory>(),
                settings));

        serviceRegistry.Register<IData, ISnesData>(CreateSnesApiWithData);
        
        serviceRegistry.Register<IProjectImportDefaultSettingsFactory, SnesDefaultSettingsFactory>();
        serviceRegistry.Register<ISnesRomImportSettingsBuilder, SnesRomImportSettingsBuilder>();

        serviceRegistry.Register<ISnesRomAnalyzer, SnesRomAnalyzer>();
        serviceRegistry.Register<IVectorTableCache, CachedVectorTableEntries>();
        
        RegisterMigrations(serviceRegistry);

        RegisterSampleDataServices(serviceRegistry);
    }

    private static ISnesData CreateSnesApiWithData(IServiceFactory serviceFactory, IData data) =>
        new SnesApi(data);

    private static void RegisterMigrations(IServiceRegistry serviceRegistry)
    {
        // list all SNES-specific migrations here (there can be multiple migration classes here,
        // they'll be applied by their internal ordering)
        //
        // note: for registration it's important to give each of these a unique NAME so we can get all of them and apply in order.
        serviceRegistry.Register<IMigration, MigrationBugfix050JapaneseText>("migrate_100_to_101");
        serviceRegistry.Register<IMigration>(_ => new MigrationNoOp { AppliesToSaveVersion = 101 }, "migrate_101_to_102");
    }

    private static void RegisterSampleDataServices(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.Register<ISampleDataFactory, SnesSampleRomDataFactory>();
        serviceRegistry.Register<IDataFactory, SnesSampleRomDataFactory>("SampleData");
        
        // TODO: does the below line work instead of the two above?
        // serviceRegistry.Register<ISampleDataFactory, SnesSampleRomDataFactory>("SampleData");

        serviceRegistry.Register(CreateSampleProject, "SampleProject");

        serviceRegistry.Register<ISnesSampleProjectFactory>(factory =>
            new SnesSampleProjectFactory(
                factory.GetInstance<IProjectFactory>("SampleProject")
            ));
    }

    private static IProjectFactory CreateSampleProject(IServiceFactory factory) => 
        new ProjectFactory(factory.GetInstance<IDataFactory>("SampleData"));
}