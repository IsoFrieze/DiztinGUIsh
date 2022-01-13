using Diz.Core.model.snes;

namespace Diz.Core.model.project;

public class DataFactory : IDataFactory
{
    // TODO: make this be IData not just Data. it will require a lot more rework though
    // to update all client code to only use IData and ISnesApi<IData>, so, for now, skipping that.
    public Data Create() => new Data();
}