using Diz.Core;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;
using JetBrains.Annotations;

namespace Diz.Cpu._65816;

[UsedImplicitly]
public class DataAddSnesApiDecorator(IDataFactory baseDataFactory, Func<IData, ISnesApi<IData>> createSnesApi) : IDataFactory
{
    public Data Create()
    {
        var data = baseDataFactory.Create();
        data.Apis.AddIfDoesntExist(createSnesApi(data));
        return data;
    }
}