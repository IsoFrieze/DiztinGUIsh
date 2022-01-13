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
        serviceRegistry.Decorate<IDataFactory, DataAddSnesApiDecorator>();

        serviceRegistry.Register<ImportRomSettings, IProjectFactoryFromRomImportSettings>((factory, settings) =>
            new SnesProjectFactoryFromRomImportSettings(
                factory.GetInstance<IProjectFactory>(),
                settings));
        
        // list migrations (there can be multiple migration classes here, they'll be applied in order)
        serviceRegistry.Register<IMigration, MigrationBugfix050JapaneseText>();
        
        // TODO: consider using a named service for this sample instead
        serviceRegistry.Register<ISnesSampleProjectFactory, SnesSampleProjectFactory>();
        serviceRegistry.Register<ISampleDataFactory, SnesSampleRomDataFactory>();
    }
}