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
        serviceRegistry.Register<IMigration, MigrationBugfix050JapaneseText>("MigrationBugfix050JapaneseText");

        serviceRegistry.Register<ISnesData, SnesApi>();
        
        // when we create a IData registration, add a SNES API component to it
        serviceRegistry.Initialize(
            registration => registration.ServiceType == typeof(IData),
            (factory, instance) => InjectSnesApi(factory, (IData)instance));

        serviceRegistry.Register<ImportRomSettings, IProjectFactoryFromRomImportSettings>((factory, settings) =>
            new SnesProjectFactoryFromRomImportSettings(
                factory.GetInstance<IProjectFactory>(),
                settings));
        
        // TODO: consider using a named service for this instead
        serviceRegistry.Register<ISnesSampleProjectFactory, SnesSampleProjectFactory>();
        serviceRegistry.Register<ISampleDataFactory, SnesSampleRomDataFactory>();
        
        // list migrations (there can be multiple migration classes here, they'll be applied in order)
        serviceRegistry.Register<IMigration, MigrationBugfix050JapaneseText>();
    }

    private static void InjectSnesApi(IServiceFactory factory, IData data) => 
        data.Apis.Add(factory.GetInstance<ISnesData>());
}