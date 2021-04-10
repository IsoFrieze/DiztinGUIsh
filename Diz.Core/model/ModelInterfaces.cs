using System.Collections.Generic;
using Diz.Core.util;

namespace Diz.Core.model
{
    public interface IReadOnlyLabelProvider
    {
        public IEnumerable<KeyValuePair<int, IReadOnlyLabel>> Labels { get; }

        Label GetLabel(int snesAddress);
        string GetLabelName(int snesAddress);
        string GetLabelComment(int snesAddress);
    }

    public interface IReadOnlySnesRom
    {
        RomMapMode RomMapMode { get; }
        RomSpeed RomSpeed { get; }

        public T GetOneAnnotationAtPc<T>(int pcOffset) where T : Annotation, new();
        
        public IReadOnlyLabelProvider LabelProvider { get; }

        Comment GetComment(int offset);
        string GetCommentText(int offset);

        int GetRomSize();
        int GetBankSize();

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
}