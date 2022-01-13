using Diz.Core;
using Diz.Core.model.snes;
using JetBrains.Annotations;

namespace Diz.Cpu._65816;

[UsedImplicitly]
public class DataAddSnesApiDecorator : IDataFactory
{
    private readonly IDataFactory baseDataFactory;
    private readonly ISnesData snesApi;

    public DataAddSnesApiDecorator(IDataFactory baseDataFactory, ISnesData snesApi)
    {
        this.baseDataFactory = baseDataFactory;
        this.snesApi = snesApi;
    }

    public Data Create()
    {
        var data = baseDataFactory.Create();
        data.Apis.Add(snesApi);
        return data;
    }
}