namespace Diz.Core.Interfaces;

public interface ICommentTextProvider
{
    // search both ROM comments and applicable label comments
    string GetCommentText(int snesAddress);
    
    // search just ROM comments
    string? GetComment(int snesAddress);
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