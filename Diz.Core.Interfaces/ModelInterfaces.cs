using System.Collections.Specialized;
using System.ComponentModel;
using Diz.Core.model;
using IX.Observable;
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

    public interface IInstructionGettable
    {
        int GetInstructionLength(int offset);
        string GetInstruction(int offset);
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
    public interface ITemporaryLabelProvider
    {
        // add a temporary label which will be cleared out when we are finished the export
        // this should not add a label if a real label already exists.
        public void AddTemporaryLabel(int snesAddress, IAnnotationLabel label);
        public void ClearTemporaryLabels();
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

    public interface IData : 
        INotifyPropertyChanged,
        IReadOnlyByteSource,
        IRomMapProvider,
        IRomBytesProvider,
        IMarkable,
        IArchitectureSettable,
        IArchitectureGettable,
        ICommentTextProvider
    {
        IArchitectureApiProvider ArchProvider { get; }
        
        ILabelServiceWithTempLabels Labels { get; }
        
        // TODO: temp hack for serialization, do this better somehow.
        Dictionary<int, IAnnotationLabel> LabelsSerialization { get; }
        
        public ObservableDictionary<int, string> Comments { get; }
    }

    public static class DataExtensions
    {
        public static T? GetApi<T>(this IData @this) where T : class => 
            @this.ArchProvider.GetApi<T>();
    }
    
    public interface ISnesCachedVerificationInfo
    {
        public string InternalRomGameName { get; set; }
        public uint InternalCheckSum { get; set; }
    }

    // API for a specific architecture API (like SNES, genesis, etc)
    public interface IArchitectureApi
    {
        
    }

    public interface IArchitectureApiProvider
    {
        IEnumerable<IArchitectureApi> Apis { get; }
        bool AddApiProvider(IArchitectureApi provider);
    }
    
//     public interface IReadOnlySnesRom :
//         IInstructionGettable,
//         IReadOnlyByteSource,
//         IRomMapProvider,
//         ICommentTextProvider,
//         IReadOnlyLabels,
//         ISnesAddressConverter,
//         ISnesIntermediateAddress,
//         IRomSize
//     
// #if DIZ_3_BRANCH
//         ,ICommentProvider,
//         IAnnotationProvider,
//         IByteGraphProvider
// #endif
//     {
//     }

    public interface ISnesIntermediateAddress
    {
        int GetIntermediateAddressOrPointer(int offset);
        int GetIntermediateAddress(int offset, bool resolve = false);   
    }
    
    public interface ISnesAddressConverter
    {
        int ConvertPCtoSnes(int offset);
        int ConvertSnesToPc(int offset);
    }

    public static class ArchitectureProviderExtensions
    {
        public static T GetApi<T>(this IArchitectureApiProvider @this) where T : class
        {
            try
            {
                return (@this.Apis.Single(x => x is T) as T)!;
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"No API found of type {typeof(T).Name}", ex);
            }
        }
    }
}