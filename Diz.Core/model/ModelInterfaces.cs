using System.Collections.Generic;
using System.Collections.Specialized;
using Diz.Core.export;
using Diz.Core.util;

namespace Diz.Core.model
{
    public interface ILabelProvider
    {
        public IEnumerable<KeyValuePair<int, Label>> Labels { get; }

        Label GetLabel(int offset);
        string GetLabelName(int offset);
        string GetLabelComment(int offset);
    }
    
    public interface IReadOnlySnesRom : ILabelProvider
    {
        RomMapMode RomMapMode { get; }
        RomSpeed RomSpeed { get; }

        public T GetOneAnnotationAtPc<T>(int pcOffset) where T : Annotation, new();

        Comment GetComment(int offset);
        string GetCommentText(int offset);

        int GetRomSize();

        int GetRomByte(int offset);
        public int GetRomWord(int offset);
        public int GetRomLong(int offset);
        public int GetRomDoubleWord(int offset);
        
        int ConvertPCtoSnes(int offset);
        int ConvertSnesToPc(int offset);
        
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
    
    public interface ISnesCpuMarker
    {
        // future
        int Step(int offset, bool branch, bool force, int prevOffset);
        void SetMFlag(int offset, bool b);
        void SetXFlag(int offset, bool b);
    }
    
    // TODO: add this.
    /*public interface ISnesInstructionWriter
    {
        void Set_WhateverXYZ(int offset); // stuff that's in data
    }*/

    // public interface IReadOnlySnesData : IReadOnlySnesRom, ISnesCpuMarker
    // {
    //     // future
    // }
    
    // -------------------
    
    
    /*public interface IByte : INotifyPropertyChangedExt
    {
        public byte Value { get; }
        
        // offset into parent container
        public IByteList Container { get; }
        public int Offset { get; }
    }
    
    public interface IAnnotation : INotifyPropertyChangedExt
    {
        
    }

    public interface IByteList : IList<IByte>, INotifyCollectionChanged
    {
        
    }

    public interface IByteSource : INotifyPropertyChangedExt
    {
        public IList<IList<IAnnotation>> Annotations { get; }
        public IByteList Bytes { get; }
    }*/
}