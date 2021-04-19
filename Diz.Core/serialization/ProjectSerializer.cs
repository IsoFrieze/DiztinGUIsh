using System.IO;
using Diz.Core.model;

namespace Diz.Core.serialization
{
    public abstract class ProjectSerializer
    {
        protected const string DizWatermark = "DiztinGUIsh";

        public abstract byte[] Save(Project project);
        public abstract (Project project, string warning) Load(byte[] data);

        public void SaveToFile(Project project, string filename)
        {
            File.WriteAllBytes(filename, Save(project));
        }
        
        public (Project project, string warning) LoadFromFile(string filename)
        {
            return Load(File.ReadAllBytes(filename));
        }

        #if HEAVY_LOADSAVE_VERIFICATION
        protected static void DebugVerifyProjectEquality(Project project1, Project project2, bool deepCut = true)
        {
            if (deepCut)
            {
                for (var i = 0; i < project1.Data.RomByteSource?.Bytes.Count; ++i)
                {
                    // TMP DISABLE // Debug.Assert(project1.Data.RomBytes[i].EqualsButNoRomByte(project2.Data.RomBytes[i]));
                }

                Debug.Assert(
                    project1.Data.RomByteSource != null && 
                    project1.Data.RomByteSource.Bytes.Equals(project2.Data.RomByteSource.Bytes)
                    );
                
                Debug.Assert(project1.Data.Equals(project2.Data));
            }
            Debug.Assert(project1.Equals(project2));
        }
        #endif
    }
}
