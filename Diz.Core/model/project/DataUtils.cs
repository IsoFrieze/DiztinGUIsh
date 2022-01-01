using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using Diz.Core.util;
using LightInject;

namespace Diz.Core.model.project;

public static class DataUtils
{
    // TODO: make this be IData not just Data. it will require a lot more rework though
    // to update all client code to only use IData and ISnesApi<IData>, so, for now, skipping that.
    public static Data FactoryCreate() => 
        Service.Container.GetInstance<Data>();
}