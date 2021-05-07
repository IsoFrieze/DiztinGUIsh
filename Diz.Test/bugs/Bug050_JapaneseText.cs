using System.IO;
using System.Text;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using FluentAssertions;
using Xunit;

namespace Diz.Test.bugs
{
    // https://github.com/Dotsarecool/DiztinGUIsh/issues/50
    public static class Bug050_JapaneseText
    {
        public class MemoryProjectFileManager : ProjectFileManager
        {
            public byte[] RawProjectBytes { get; set; } = { };
            protected override void WriteBytes(string filename, byte[] data) => RawProjectBytes = data;
            protected override byte[] ReadAllBytes(string filename) => RawProjectBytes;
        }

        public class Bug50ProjectFileManager : MemoryProjectFileManager
        {
            public event ProjectXmlSerializer.SerializeEvent BeforeSerialize;
            public event ProjectXmlSerializer.SerializeEvent AfterDeserialize;

            protected override (ProjectXmlSerializer.Root xmlRoot, string warning) DeserializeWith(ProjectSerializer serializer, byte[] rawBytes)
            {
                var xmlSerializer = serializer as ProjectXmlSerializer;
                xmlSerializer!.AfterDeserialize += AfterDeserialize;

                return base.DeserializeWith(serializer, rawBytes);
            }

            protected override byte[] SerializeWith(Project project, ProjectSerializer serializer)
            {
                var xmlSerializer = serializer as ProjectXmlSerializer;
                xmlSerializer!.BeforeSerialize += BeforeSerialize;
                
                return base.SerializeWith(project, serializer);
            }
        }
        
        [Fact]
        public static void Test()
        {
            var project = LoadSaveTest.BuildSampleProject2();
            var originalGoodGameName = project.InternalRomGameName;

            var projectFileManager = new Bug50ProjectFileManager
            {
                RomPromptFn = s => 
                    throw new InvalidDataException("We should not hit this for this test, if it worked.")
            };
            
            var badGameName = ByteUtil.ReadShiftJisEncodedString(
                ByteUtil.PadCartridgeTitleBytes(
                    ByteUtil.ConvertUtf8ToShiftJisEncodedBytes("BAD")
                )
            );

            projectFileManager.BeforeSerialize += (serializer, rootElement) =>
            {
                // doctor some data before we serialize, in order to trigger the bug and the workaround
                
                // we want to invoke the migration functions because this is an older save
                rootElement.SaveVersion = 100;

                // we want to trigger the bug which happens if the saved data in the XML doesn't match the ROM.
                rootElement.Project.InternalRomGameName = badGameName;
                
                Assert.NotEqual(rootElement.Project.InternalRomGameName, rootElement.Project.Data.CartridgeTitleName);
            };

            projectFileManager.Save(project, "IGNORED");
            
            Assert.NotNull(projectFileManager.RawProjectBytes);
            
            // ----------
            // Save is done! now reload it.
            // ----------

            projectFileManager.AfterDeserialize += (serializer, root) =>
            {
                // here's the bug: the deserialized data is wrong and needs to be fixed.
                // this is invoked before the post-serialize migrations run, so will still be wrong.
                // outside this callback, it should get fixed up automatically by the migration code.
                root.Project.InternalRomGameName.Should().Be(badGameName, "Migrations to fix the bug haven't run yet.");
                root.SaveVersion.Should().Be(100, "It was saved with the older file format");
            };
            
            var result = projectFileManager.Open("IGNORED");
            
            result.project.InternalRomGameName.Should().NotBe(badGameName, "Migrations should have fixed this");
            result.project.InternalRomGameName.Should().Be(originalGoodGameName, "it should be the original cartridge name after the automatic fix");
        }
    }
}