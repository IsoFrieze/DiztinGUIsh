using Diz.Core;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using JetBrains.Annotations;

namespace Diz.Cpu._65816;

[UsedImplicitly]
public class DataAddSnesApiDecorator : IDataFactory
{
    private readonly IDataFactory baseDataFactory;
    private readonly Func<IData, ISnesData> createSnesApi;

    public DataAddSnesApiDecorator(IDataFactory baseDataFactory, Func<IData, ISnesData> createSnesApi)
    {
        this.baseDataFactory = baseDataFactory;
        this.createSnesApi = createSnesApi;
    }

    public Data Create()
    {
        var data = baseDataFactory.Create();
        data.Apis.AddIfDoesntExist(createSnesApi(data));
        return data;
    }
}