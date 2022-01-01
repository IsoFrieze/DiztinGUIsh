using Diz.Core.Interfaces;
using Diz.Core.model.snes;
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

        // TODO: switch this over to IData eventually.
        serviceRegistry.Register(factory =>
        {
            var data = Data.CreateNew();
            data.ArchProvider.AddApiProvider(new SnesApi(data));
            return data;
        });
    }
}