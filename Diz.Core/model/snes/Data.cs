using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using Diz.Core.Interfaces;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.model.snes;

public class Data : IData
{
    [XmlIgnore] public IDataStoreProvider<IArchitectureApi> Apis { get; } = new DataStoreProvider<IArchitectureApi>();
    public IDataStoreProvider<IDataTag> Tags { get; } = new DataStoreProvider<IDataTag>();

    private SortedDictionary<int, string> comments;
    private ObservableCollection<IRegion> regions = [];
    private RomBytes romBytes;

    // NOTE: snes specific stuff (rom map mode/speed) should eventually be removed from here.
    // this class should be a generic base class for all systems (snes, nes, sega, whatever).
    // for now we're in transition.
    // .. also, same thing with log generation stuff.

    // don't modify these directly, always go through the public properties so
    // other objects can subscribe to modification notifications
    private RomMapMode romMapMode;
    private RomSpeed romSpeed = RomSpeed.Unknown;

    // Note: order of these public properties matters for the load/save process. Keep 'RomBytes' LAST
    // TODO: should be a way in the XML serializer to control the order, remove this comment
    // when we figure it out.
    public RomMapMode RomMapMode
    {
        get => romMapMode;
        set => this.SetField(PropertyChanged, ref romMapMode, value);
    }

    public RomSpeed RomSpeed
    {
        get => romSpeed;
        set => this.SetField(PropertyChanged, ref romSpeed, value);
    }

    // next 2 dictionaries store in SNES address format (since memory labels can't be represented as a PC address)
    public SortedDictionary<int, string> Comments
    {
        get => comments;
        set => this.SetField(PropertyChanged, ref comments, value);
    }

    // for deserialization/loading in Diz2.0
    // TODO: this is kind of a hack needs rework. would be better to ditch this and write some kind of custom
    // deserializer that handles this instead
    public Dictionary<int, IAnnotationLabel> LabelsSerialization
    {
        get => new(Labels.Labels);
        set => Labels.SetAll(value);
    }
    
    // RomBytes stored as PC file offset addresses (since ROM will always be mapped to disk)
    public RomBytes RomBytes
    {
        get => romBytes;
        set => this.SetField(PropertyChanged, ref romBytes, value);
    }
    IRomBytes<IRomByte> IRomBytesProvider.RomBytes => romBytes;
    
    public ObservableCollection<IRegion> Regions
    {
        get => regions;
        set => this.SetField(PropertyChanged, ref regions, value);
    }
    [XmlIgnore] ObservableCollection<IRegion> IRegionProvider.Regions => Regions;


    [XmlIgnore] public bool RomBytesLoaded { get; set; }

    public Data()
    {
        comments = new SortedDictionary<int, string>();
        Labels = new LabelsServiceWithTemp(this);
        romBytes = new RomBytes();
    }

    public int GetRomSize() =>
        RomBytes?.Count ?? 0;

    public Architecture GetArchitecture(int i) => RomBytes[i].Arch;
    public void SetArchitecture(int i, Architecture arch) => RomBytes[i].Arch = arch;

    [CanBeNull]
    public string GetComment(int snesAddress) =>
        Comments.GetValueOrDefault(snesAddress);

    public string GetCommentText(int snesAddress)
    {
        // option 1: use the comment text first
        var comment = GetComment(snesAddress);
        if (!string.IsNullOrEmpty(comment))
            return comment;

        // option 2: if a real comment doesn't exist, try see if our label itself has a comment attached, display that.
        // TODO: this is convenient for display but might mess up setting. we probably should do this
        // only in views, remove from here.
        return Labels.GetLabelComment(snesAddress) ?? "";
    }

    public void AddComment(int i, string v, bool overwrite)
    {
        if (v == null)
        {
            Comments.Remove(i);
        }
        else
        {
            if (Comments.ContainsKey(i) && overwrite)
                Comments.Remove(i);

            Comments.TryAdd(i, v);
        }
    }

    public byte? GetRomByte(int pcOffset)
    {
        return pcOffset >= RomBytes.Count ? null : RomBytes[pcOffset].Rom;
    }

    public byte? GetSnesByte(int snesAddress)
    {
        return GetRomByte(ConvertSnesToPc(snesAddress));
    }

    public int? GetRomWord(int offset)
    {
        if (offset + 1 >= GetRomSize())
            return null;

        var rb1Null = GetRomByte(offset);
        var rb2Null = GetRomByte(offset + 1);
        if (!rb1Null.HasValue || !rb2Null.HasValue)
            return null;

        return rb1Null + (rb2Null << 8);
    }

    public int? GetRomLong(int offset)
    {
        if (offset + 2 >= GetRomSize())
            return null;

        var romWord = GetRomWord(offset);
        var rb3Null = GetRomByte(offset + 2);
        if (!romWord.HasValue || !rb3Null.HasValue)
            return null;

        return romWord + (rb3Null << 16);
    }

    public int? GetRomDoubleWord(int offset)
    {
        if (offset + 3 >= GetRomSize())
            return null;

        var romLong = GetRomLong(offset);
        var rb4Null = GetRomByte(offset + 3);
        if (!romLong.HasValue || !rb4Null.HasValue)
            return null;

        return romLong + (rb4Null << 24);
    }

    public int ConvertPCtoSnes(int offset)
    {
        return RomUtil.ConvertPCtoSnes(offset, RomMapMode, RomSpeed);
    }

    public int ConvertSnesToPc(int address)
    {
        return RomUtil.ConvertSnesToPc(address, RomMapMode, GetRomSize());
    }

    public int Mark(Action<int> markAction, int offset, int count)
    {
        int i, size = GetRomSize();
        for (i = 0; i < count && offset + i < size; i++)
            markAction(offset + i);

        return offset + i < size
            ? offset + i
            : size - 1;
    }

    // get the actual ROM file bytes (i.e. the contents of the SMC file on the disk)
    // note: don't save these anywhere permanent because ROM data is usually copyrighted.
    public IEnumerable<byte> GetFileBytes() =>
        RomBytes.Select(b => b.Rom);

    #region Equality

    protected bool Equals(Data other)
    {
        return Labels.Equals(other.Labels) && RomMapMode == other.RomMapMode && RomSpeed == other.RomSpeed &&
               Comments.SequenceEqual(other.Comments) && RomBytes.Equals(other.RomBytes);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == this.GetType() && Equals((Data)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Labels.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)RomMapMode;
            hashCode = (hashCode * 397) ^ (int)RomSpeed;
            hashCode = (hashCode * 397) ^ Comments.GetHashCode();
            hashCode = (hashCode * 397) ^ RomBytes.GetHashCode();
            return hashCode;
        }
    }

    #endregion

    [XmlIgnore] public LabelsServiceWithTemp Labels { get; }
    [XmlIgnore] ILabelServiceWithTempLabels IData.Labels => Labels;

    [CanBeNull] public IRegion CreateNewRegion() => new Region();
    [CanBeNull]  public IRegion GetRegion(int snesAddress)
    {
        return Regions
            .Where(region => snesAddress >= region.StartSnesAddress && snesAddress < region.EndSnesAddress)
            .OrderByDescending(region => region.Priority)
            .FirstOrDefault();
    }
    
    public event PropertyChangedEventHandler PropertyChanged;
}

public class Region : IRegion
{
    private int startSnesAddress;
    private int endSnesAddress;
    private string regionName;
    private string contextToApply;
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

    public event PropertyChangedEventHandler PropertyChanged;
}