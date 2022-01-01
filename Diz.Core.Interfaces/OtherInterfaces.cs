using IX.Observable;

namespace Diz.Core.Interfaces;

public interface ICommentTextProvider
{
    string GetCommentText(int snesAddress);
}

#if DIZ_3_BRANCH
    public interface IAnnotationProvider
    {
        public T GetOneAnnotationAtPc<T>(int pcOffset) where T : Annotation, new();   
}

    public interface IByteGraphProvider
    {
        ByteEntry BuildFlatByteEntryForSnes(int snesAddress);
        ByteEntry BuildFlatByteEntryForRom(int snesAddress);
    }
#endif