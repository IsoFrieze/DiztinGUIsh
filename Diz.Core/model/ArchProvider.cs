using System.Collections.Generic;
using Diz.Core.Interfaces;

namespace Diz.Core.model;

public class ArchProvider : IArchitectureApiProvider
{
    private readonly List<IArchitectureApi> apis = new();

    public IEnumerable<IArchitectureApi> Apis => apis;

    public bool AddApiProvider(IArchitectureApi provider)
    {
        if (apis.Exists(x => x.GetType() == provider.GetType()))
            return false;
        
        apis.Add(provider);
        return true;
    }
}