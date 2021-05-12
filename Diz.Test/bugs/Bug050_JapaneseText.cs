using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using Diz.Test.Tests.SerializationTests;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Diz.Test.bugs
{
    // https://github.com/Dotsarecool/DiztinGUIsh/issues/50
    public static class Bug050JapaneseText
    {
        public class MemoryProjectFileManager : ProjectFileManager
        {
            public byte[] RawProjectBytes { get; set; } = Array.Empty<byte>();
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

            protected override ProjectXmlSerializer CreateProjectXmlSerializer()
            {
                return new ProjectXmlSerializerBug50();
            }

            public class ProjectXmlSerializerBug50 : ProjectXmlSerializer
            {
                
            }
        }

        public class Harness
        {
            public bool ExpectedMitigationApplied = true;
            public Project Project = LoadSaveTest.BuildSampleProject2();
            [CanBeNull] public string OverrideGameName;
            public bool ForceOlderSaveVersionWhichShouldFix = true;
        }
        
        public static IEnumerable<object[]> Harnesses => new List<Harness>
        {
            // we should see the mitigation code fix this scenario up correctly
            new()
            {
                ExpectedMitigationApplied = true,
                OverrideGameName = "BAD",
            },
            
            // in a newer save format, there shouldn't be a bug anyomre,
            // so we don't expect the mitigation code to run.
            new()
            {
                ExpectedMitigationApplied = false,
                OverrideGameName = "BAD",
                ForceOlderSaveVersionWhichShouldFix = false,
            },
        }.Select(x=>new []{x});
        
        [Theory]
        [MemberData(nameof(Harnesses))]
        public static void TestMitigation(Harness harness)
        {
            var project = harness.Project;
            var originalGoodGameName = project.InternalRomGameName;
            var badGameName = harness.OverrideGameName == null ? null : 
                ByteUtil.ReadShiftJisEncodedString(
                ByteUtil.PadCartridgeTitleBytes(
                    ByteUtil.ConvertUtf8ToShiftJisEncodedBytes(harness.OverrideGameName)
                )
            );
            var saveVersionToUse = harness.ForceOlderSaveVersionWhichShouldFix ? 100 : 101;

            var projectFileManager = new Bug50ProjectFileManager
            {
                RomPromptFn = _ => 
                    throw new InvalidDataException("UNIT TEST SHOULD NOT HIT GET HERE.")
            };

            projectFileManager.BeforeSerialize += (_, rootElement) =>
            {
                // doctor some data before we serialize, in order to trigger the bug and the workaround
                
                // we want to invoke the migration functions because this is an older save
                rootElement.SaveVersion = saveVersionToUse;

                // we want to trigger the bug which happens if the saved data in the XML doesn't match the ROM.
                rootElement.Project.InternalRomGameName = badGameName;
                
                if (badGameName != null)
                    Assert.NotEqual(rootElement.Project.InternalRomGameName, rootElement.Project.Data.CartridgeTitleName);
            };

            projectFileManager.Save(project, "IGNORED");
            
            Assert.NotNull(projectFileManager.RawProjectBytes);
            
            // ----------
            // Save is done! now reload it.
            // ----------

            projectFileManager.AfterDeserialize += (_, root) =>
            {
                root.SaveVersion.Should().Be(saveVersionToUse, "It was saved with the older file format");
                
                // here's the bug: the deserialized data is wrong and needs to be fixed.
                // this is invoked before the post-serialize migrations run, so will still be wrong.
                // outside this callback, it should get fixed up automatically by the migration code.
                if (!string.IsNullOrEmpty(badGameName))
                    root.Project.InternalRomGameName.Should().Be(badGameName, "Migrations to fix the bug haven't run yet.");
            };

            Project deserializedProject = null;
            var runDeserializer = new Action(() =>
            {
                (deserializedProject, _) = projectFileManager.Open("IGNORED");
            });

            if (!harness.ExpectedMitigationApplied)
            {
                // this is what the original bug would do
                runDeserializer.Invoking(a => a()).Should().Throw<InvalidDataException>()
                    .WithMessage(
                        "Verification check: The project file requires the linked ROM's SNES header to have a cartridge title name*this doesn't match the title in the ROM file,*");
            }
            else
            {
                // this is what it looks like when the bug is it's fixed
                runDeserializer();
                deserializedProject.Should().NotBeNull("it shold have deserialized correctly.");
                deserializedProject.InternalRomGameName.Should().NotBe(badGameName,
                    "Migrations should have fixed this");
                deserializedProject.InternalRomGameName.Should().Be(originalGoodGameName,
                    "the automatic fix should have set this name back to the correct one");
            }
        }
    }
}