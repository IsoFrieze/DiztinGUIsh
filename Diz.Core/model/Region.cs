#nullable enable
using System.ComponentModel;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Core.model.snes;

public class Region : IRegion
{
    private int startSnesAddress;
    private int endSnesAddress;
    private string regionName = "";
    private string contextToApply = "";
    private int priority;
    private bool exportSeparateFile;

    public int StartSnesAddress
    {
        get => startSnesAddress;
        set => this.SetField(PropertyChanged, ref startSnesAddress, value);
    }

    public int EndSnesAddress
    {
        get => endSnesAddress;
        set => this.SetField(PropertyChanged, ref endSnesAddress, value);
    }

    public string RegionName
    {
        get => regionName;
        set => this.SetField(PropertyChanged, ref regionName, value);
    }

    public string ContextToApply
    {
        get => contextToApply;
        set => this.SetField(PropertyChanged, ref contextToApply, value);
    }

    public int Priority
    {
        get => priority;
        set => this.SetField(PropertyChanged, ref priority, value);
    }
    
    public bool ExportSeparateFile
    {
        get => exportSeparateFile;
        set => this.SetField(PropertyChanged, ref exportSeparateFile, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}