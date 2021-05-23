using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Diz.Core.model.byteSources;
using Diz.Core.util;

namespace Diz.Core.model
{
    public interface IReadOnlyLabelProvider
    {
        public IEnumerable<KeyValuePair<int, Label>> Labels { get; }

        Label GetLabel(int snesAddress);
        string GetLabelName(int snesAddress);
        string GetLabelComment(int snesAddress);
    }
    
    public interface IReadOnlyByteSource
    {
        byte? GetRomByte(int offset);
        int? GetRomWord(int offset);
        int? GetRomLong(int offset);
        int? GetRomDoubleWord(int offset);
    }

    public interface ICommentProvider
    {
        Comment GetComment(int offset);
        string GetCommentText(int snesAddress);
    }

    public interface IRomMapProvider
    {
        RomMapMode RomMapMode { get; }
        RomSpeed RomSpeed { get; }
    }

    public interface IAnnotationProvider
    {
        public T GetOneAnnotationAtPc<T>(int pcOffset) where T : Annotation, new();   
    }

    public interface IByteGraphProvider
    {
        ByteEntry BuildFlatByteEntryForSnes(int snesAddress);
        ByteEntry BuildFlatByteEntryForRom(int snesAddress);
    }

    public interface IReadOnlyLabels
    {
        public IReadOnlyLabelProvider Labels { get; }
    }

    public interface IReadOnlySnesRom : 
        IReadOnlyByteSource,
        ISnesAddressConverter, 
        ICommentProvider,
        IRomMapProvider,
        IAnnotationProvider,
        IByteGraphProvider,
        IReadOnlyLabels,
        IReadOnlySnesRomBase
    {
    }

    public interface IReadOnlySnesRomBase
    {
        int GetRomSize();
        int GetBankSize();

        bool GetMFlag(int offset);
        bool GetXFlag(int offset);
        InOutPoint GetInOutPoint(int offset);
        int GetInstructionLength(int offset);
        string GetInstruction(int offset);
        int GetDataBank(int offset);
        int GetDirectPage(int offset);
        FlagType GetFlag(int offset);
        
        int GetIntermediateAddressOrPointer(int offset);
        int GetIntermediateAddress(int offset, bool resolve = false);   
    }

    public interface ISnesAddressConverter
    {
        int ConvertPCtoSnes(int offset);
        int ConvertSnesToPc(int offset);
    }
    
    public interface IDataManager : INotifyPropertyChanged
    {
        
    }

    public interface IDizObservable<T> : ICollection<T>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        
    }
    
    public interface IDizObservableList<T> : IDizObservable<T>
    {
        
    }
}