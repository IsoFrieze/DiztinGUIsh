using System.Diagnostics;
using System.IO;
using Diz.Core.model;
using Diz.Core.serialization.xml_serializer;

namespace Diz.Core.serialization;

public abstract class ProjectSerializer
{
    protected ProjectSerializer(IMigrationRunner migrationRunner)
    {
        MigrationRunner = migrationRunner;
    }

    public const string DizWatermark = "DiztinGUIsh";

    public abstract byte[] Save(Project project);
    public abstract ProjectOpenResult Load(byte[] rawBytes);

    public void SaveToFile(Project project, string filename)
    {
        File.WriteAllBytes(filename, Save(project));
    }
        
    public ProjectOpenResult LoadFromFile(string filename)
    {
        return Load(File.ReadAllBytes(filename));
    }

    protected static void DebugVerifyProjectEquality(Project project1, Project project2, bool deepCut = true)
    {
        if (deepCut)
        {
            for (var i = 0; i < project1.Data.RomBytes.Count; ++i)
            {
                Debug.Assert(project1.Data.RomBytes[i].EqualsButNoRomByte(project2.Data.RomBytes[i]));
            }

            Debug.Assert(project1.Data.RomBytes.Equals(project2.Data.RomBytes));
            Debug.Assert(project1.Data.Equals(project2.Data));
        }
        Debug.Assert(project1.Equals(project2));
    }

    // caution: don't cache, these may not be reliable for resetting themselves each time they run.
    public IMigrationRunner MigrationRunner { get; }
}