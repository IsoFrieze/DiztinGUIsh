using System.Collections.ObjectModel;

namespace Diz.Core.Interfaces;

public class OperandOverride
{
    // completely override the operand text with this user-specified text
    // this is the complete wild west: no checks will be done, etc.
    public string TextToOverride { get; set; } = "";

    public string GetTextOverrideAsLabel(bool chopExclamationPoint)
    {
        if (TextToOverride.Length == 0)
            return "";
            
        return chopExclamationPoint ? TextToOverride.TrimStart('!') : TextToOverride;
    }
        
    // if true, never print a label (always print the raw hex)
    // useful for things like PEA or PER instructions which may falsely grab labels
    public bool ForceOnlyShowRawHex { get; set; }
        
    // if true, then this particular label WONT create a temporary label
    // at its original offset (useful for things like PTR_ or DATA_ destinations where
    // the label value here is used for accessing memory that's really not related to it.
    // for instance, if a game is doing "LDA.L $C00000, X", and accesisng lots of locations using 
    // different values in X, then, we might not want to stick a "DATA_" label at $C00000
    public bool DontGenerateTemporaryLabelAtDestination { get; set; }

    public enum FormatOverride
    {
        None,
        AsDecimal,
        // add more as desired
    }

    public enum IncSrcOverride
    {
        None,
        IncSrcStart,
        IncSrcEnd,
    }

    public FormatOverride ConstantFormatOverride { get; set; } = FormatOverride.None;
    public IncSrcOverride IncludeSrc { get; set; } = IncSrcOverride.None;
}

public interface IRegionProvider
{
    ObservableCollection<IRegion> Regions { get; }
    IRegion? GetRegion(int snesAddress);

    // create a new region (doesn't add it to collection)
    IRegion? CreateNewRegion();
}

public interface ICommentTextProvider
{
    // search both ROM comments and applicable label comments
    string GetCommentText(int snesAddress);
    
    // search just ROM comments
    string? GetComment(int snesAddress);
}

public interface ICpuDirectiveProvider
{
    public OperandOverride? GetSpecialDirectiveOverrideFromComments(int offset);
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

// utility for getting info about the running app
public interface IAppVersionInfo
{
    enum AppVersionInfoType
    {
        Version,
        FullDescription,
    }
    
    string GetVersionInfo(AppVersionInfoType type);
}