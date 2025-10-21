using System.Collections.Specialized;
using System.ComponentModel;

#if DIZ_3_BRANCH
using Diz.Core.model.byteSources;
#endif

namespace Diz.Core.Interfaces
{
    public interface IReadOnlyByteSource
    {
        byte? GetRomByte(int offset);
        int? GetRomWord(int offset);
        int? GetRomLong(int offset);
        int? GetRomDoubleWord(int offset);
    }

    public interface IInOutPointSettable
    {
        public void SetInOutPoint(int i, InOutPoint point);
        public void ClearInOutPoint(int i);
        
        void RescanInOutPoints();
    }
    
    public interface IInOutPointGettable
    {
        InOutPoint GetInOutPoint(int offset);   
    }

    public interface IRomByteFlagsGettable
    {
        int GetMxFlags(int i);
        bool GetMFlag(int i);
        bool GetXFlag(int i);

        int GetDataBank(int offset);
        int GetDirectPage(int offset);
        FlagType GetFlag(int offset);
    }

    public interface IRomByteFlagsSettable
    {
        void SetMxFlags(int i, int mx);
        void SetMFlag(int romOffset, bool value);
        void SetXFlag(int romOffset, bool value);

        void SetDataBank(int romOffset, int result);
        void SetDirectPage(int romOffset, int result);
        void SetFlag(int offset, FlagType flagType);
    }

    public interface IArchitectureGettable 
    {
        public Architecture GetArchitecture(int i);
    }
    
    public interface IArchitectureSettable 
    {
        public void SetArchitecture(int i, Architecture arch);
    }

    public interface IRomMapProvider
    {
        // eventually, remove the 'set' if we can just to make this read-only
        RomMapMode RomMapMode { get; set; }
        RomSpeed RomSpeed { get; set; }
    }

    public interface IRomSize
    {
        int GetRomSize();
        int GetBankSize();
    }

    public interface ISnesBankInfo
    {
        int GetNumberOfBanks();
        string GetBankName(int bankIndex);
    }
    
    public class CpuInstructionDataFormatted
    {
        // the final text that should be printed
        // (most of the time, this is the main thing you're after)
        public string FullGeneratedText { get; set; } = "";
        
        // stuff below is the info that went into generating the above fully generated text
        // useful if you need more context. feel free to expose/save more into this struct as you need it
        
        // if the operand was overridden, what was the overridden text?
        public string OverriddenOperand1 { get; set; } = "";
        public string OverriddenOperand2 { get; set; } = "";
        
        // if the operand was overridden, what was the NON-overridden original value?
        public string OriginalNonOverridenOperand1 { get; set; } = "";
        public string OriginalNonOverridenOperand2 { get; set; } = "";
    }
    
    public interface IInstructionGettable
    {
        int GetInstructionLength(int offset);
        string GetInstructionStr(int offset);
        public CpuInstructionDataFormatted GetInstructionData(int offset);
    }
    
    public interface IMarkable
    {
        int Mark(Action<int> markAction, int offset, int count);
    }

    public interface IRomByteBase
    {
        byte Rom { get; set; }
        int Offset { get; }
    }

    public interface ISnesRomByte : INotifyPropertyChanged
    {
        byte DataBank { get; set; }
        int DirectPage { get; set;}
        bool XFlag { get; set;}
        bool MFlag { get; set;}
        FlagType TypeFlag { get; set; }
        Architecture Arch { get; set; }
        InOutPoint Point { get; set; }
    }
    
    public interface IRomByte : 
        IRomByteBase, 
        ISnesRomByte // eventually, need to get the ISnesRomByte out of here.
    {
        public ReaderWriterLockSlim Lock { get; }
        public bool EqualsButNoRomByte(IRomByte other);
    }

    public interface IRomBytes<out T> : IEnumerable<T>, INotifyCollectionChanged, INotifyPropertyChanged 
        where T : IRomByte 
    {
        void Clear();
        
        public IRomByte this[int i] { get; }
        
        int Count { get; }
        bool SendNotificationChangedEvents { get; set; }
        void SetBytesFrom(IReadOnlyList<byte> copyFrom, int dstStartingOffset);
    }
    
    public interface IRomBytesProvider
    {
        public IRomBytes<IRomByte> RomBytes { get; }
    }

    public static class RomBytesProviderExtension
    {
        // expensive and inefficient helper, don't use if you care about perf
        public static List<byte> CreateListRawRomBytes(this IEnumerable<IRomByte> romBytes) =>
            romBytes.Select(x => x.Rom).ToList();
    }
    
    

    // would love to redesign so we can get rid of this class and all this temporary label stuff.
    public interface ITemporaryLabelProvider : ILabelProvider
    {
        // add a temporary label which will be cleared out when we are finished the export
        // this should not add a label if a real label already exists.
        public void AddOrReplaceTemporaryLabel(int snesAddress, IAnnotationLabel label);
        public void ClearTemporaryLabels();
        void LockLabelsCache();
        void UnlockLabelsCache();
    }

    public interface ILabelService : 
        ILabelProvider,
        IReadOnlyLabelProvider
    {
    }
    
    public interface ILabelServiceWithTempLabels : 
        ILabelService,
        ITemporaryLabelProvider
    {
        
    }
// TODO: below: #if DIZ_3_BRANCH
//         ,ICommentProvider,
//         IAnnotationProvider,
//         IByteGraphProvider
// #endif
    
    public interface IData : 
        INotifyPropertyChanged,
        IReadOnlyByteSource,
        IRomMapProvider,
        IRomBytesProvider,
        IMarkable,
        IArchitectureSettable,
        IArchitectureGettable,
        ICommentTextProvider,
        IRegionProvider
    {
        IDataStoreProvider<IArchitectureApi> Apis { get; }
        IDataStoreProvider<IDataTag> Tags { get; }

        ILabelServiceWithTempLabels Labels { get; }
        
        public SortedDictionary<int, string> Comments { get; }
    }

    public static class DataExtensions
    {
        public static T? GetApi<T>(this IData @this) where T : class, IArchitectureApi => 
            @this.Apis.Get<T>();
        public static T? GetTag<T>(this IData @this) where T : class, IDataTag => 
            @this.Tags.Get<T>();
    }
    
    public interface ISnesCachedVerificationInfo
    {
        public string InternalRomGameName { get; set; }
        public uint InternalCheckSum { get; set; }
    }
    
    public interface IDataStoreProvider<T> : IEnumerable<T> where T : class
    {
        bool AddIfDoesntExist(T type);
        TSearchFor Get<TSearchFor>() where TSearchFor : class, T;
    }
    
    // // API for a specific architecture API (like SNES, genesis, etc)
    public interface IArchitectureApi
    {
        
    }
    
    public interface IDataTag
    {
        
    }

    public interface ISnesIntermediateAddress
    {
        int GetIntermediateAddressOrPointer(int offset);
        
        // -1 if not found
        int GetIntermediateAddress(int offset, bool resolve = false);
        
        bool IsMatchingIntermediateAddress(int intermediateAddress, int addressToMatch);
    }
    
    public interface ISnesAddressConverter
    {
        int ConvertPCtoSnes(int offset);
        int ConvertSnesToPc(int offset);
    }
}