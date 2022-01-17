#nullable enable

using Diz.Core.model;
using Diz.Core.model.snes;

namespace Diz.Core;

public interface IDataRange
{
    public int MaxCount { get; }
        
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
    public int RangeCount { get; set; }

    public void ManualUpdate(int newStartIndex, int newRangeCount);
}

public interface IDataFactory
{
    // TODO: eventually, make this be IData. it's a whole new refactor though. 
    Data Create();
}

// TODO: maybe make this a decorator? for IDataFactory, then get rid of it.
public interface ISampleDataFactory : IDataFactory
{

}


public interface IProjectFileAssemblyExporter
{
    bool ExportAssembly(string projectFileName);
}


public interface IProjectProvider
{
    Project? Read();
}


public interface IProjectFileOpener : IProjectProvider
{
    void SetOpenFilename(string projectFilename);
}

public static class ProjectFileProviderExtensions
{
    public static Project? ReadProjectFromFile(this IProjectFileOpener @this, string filename)
    {
        @this.SetOpenFilename(filename);
        return @this.Read();
    }
}

public interface IProjectFactoryFromRomImportSettings : IProjectProvider
{
    
}