using Diz.Core.model;

namespace Diz.Core.serialization.xml_serializer;

public class ProjectSerializedRoot
{
    // XML serializer specific metadata, top-level deserializer.
    // This is unique to JUST the XML serializer, doesn't affect any other types of serializers.
    // i.e. there is no global 'save format version' number, it's serializer-specific.
    //
    // NOTE: Please try and keep 'Root' unchanged and as generic as possible.  It's way better
    // to change 'Project'
    public int SaveVersion { get; set; } = -1;
    public string Watermark { get; set; }
    public string Extra1 { get; set; } = ""; // reserved for future use
    public string Extra2 { get; set; } = ""; // reserved for future use

    // The actual project itself. Almost any change you want to make should go in here.
    public Project Project { get; set; }
};