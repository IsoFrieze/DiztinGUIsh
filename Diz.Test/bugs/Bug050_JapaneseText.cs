#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.Test.Utils;
using FluentAssertions;
using Xunit;

namespace Diz.Test.bugs;

// https://github.com/Dotsarecool/DiztinGUIsh/issues/50
public class Bug050JapaneseText : ContainerFixture
{
    // TODO: a lot of this can probably be replaced with mocks now
    
    public class MemoryProjectFileManager : ProjectFileManager
    {
        public byte[] RawProjectBytes { get; set; } = Array.Empty<byte>();
        protected override void WriteBytes(string filename, byte[] data) => RawProjectBytes = data;
        protected override byte[] ReadAllBytes(string filename) => RawProjectBytes;

        public MemoryProjectFileManager(
            Func<IProjectXmlSerializer> projectXmlSerializerCreate,
            Func<IAddRomDataCommand> addRomDataCommandCreate) 
            : base(projectXmlSerializerCreate, addRomDataCommandCreate)
        {
        }
    }

    public class Bug50ProjectFileManager : MemoryProjectFileManager
    {
        public event IProjectXmlSerializer.SerializeEvent? BeforeSerialize;
        public event IProjectXmlSerializer.SerializeEvent? AfterDeserialize;

        protected override ProjectOpenResult DeserializeWith(IProjectSerializer serializer, byte[] rawBytes)
        {
            if (serializer is IProjectXmlSerializer xmlSerializer && AfterDeserialize != null)
                xmlSerializer.AfterDeserialize += AfterDeserialize;

            return base.DeserializeWith(serializer, rawBytes);
        }

        protected override byte[] SerializeWith(Project project, IProjectSerializer serializer)
        {
            var xmlSerializer = serializer as ProjectXmlSerializer;
            xmlSerializer!.BeforeSerialize += BeforeSerialize;
                
            return base.SerializeWith(project, serializer);
        }

        public Bug50ProjectFileManager(
            Func<IProjectXmlSerializer> projectXmlSerializerCreate,
            Func<IAddRomDataCommand> addRomDataCommandCreate) : base(
            projectXmlSerializerCreate, addRomDataCommandCreate)
        {
                
        }
    }

    public class Harness
    {
        public bool ExpectedMitigationApplied = true;
        public static Project CreateProject(ISnesSampleProjectFactory projectCreate) 
            => (projectCreate.Create() as Project)!; // TODO: don't cast
        public string? OverrideGameName;
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
            
        // in a newer save format, there shouldn't be a bug anymore,
        // so we don't expect the mitigation code to run.
        new()
        {
            ExpectedMitigationApplied = false,
            OverrideGameName = "BAD",
            ForceOlderSaveVersionWhichShouldFix = false,
        },
    }.Select(x=>new []{x});
    
    // these will be populated by dependency injection 
    private readonly ISnesSampleProjectFactory sampleProjectFactory = null!;
    private readonly Func<IProjectXmlSerializer> fnProjectSerializerCreate = null!;
    private readonly Func<IAddRomDataCommand> fnAddRomDataCommand = null!;

    [Theory, MemberData(nameof(Harnesses))]
    public void TestMitigation(Harness harness)
    {
        var project = Harness.CreateProject(sampleProjectFactory);
        var originalGoodGameName = project.InternalRomGameName;
        var badGameName = harness.OverrideGameName == null ? null : 
            ByteUtil.ReadShiftJisEncodedString(
                ByteUtil.PadCartridgeTitleBytes(
                    ByteUtil.ConvertUtf8ToShiftJisEncodedBytes(harness.OverrideGameName)
                )
            );
        var saveVersionToUse = harness.ForceOlderSaveVersionWhichShouldFix ? 100 : 101;

        var projectFileManager = new Bug50ProjectFileManager(fnProjectSerializerCreate, fnAddRomDataCommand)
        {
            RomPromptFn = s => 
                throw new InvalidDataException("We should not hit this for this test, if it worked.")
        };

        projectFileManager.BeforeSerialize += (serializer, rootElement) =>
        {
            // doctor some data before we serialize, in order to trigger the bug and the workaround
                
            // we want to invoke the migration functions because this is an older save
            rootElement.SaveVersion = saveVersionToUse;

            // we want to trigger the bug which happens if the saved data in the XML doesn't match the ROM.
            rootElement.Project.InternalRomGameName = badGameName ?? "";

            var snesApi = rootElement.Project.Data.GetSnesApi();
            snesApi.Should().NotBeNull();
                
            if (badGameName != null)
                Assert.NotEqual(rootElement.Project.InternalRomGameName, snesApi?.CartridgeTitleName ?? "");
        };

        projectFileManager.Save(project, "IGNORED");
            
        Assert.NotNull(projectFileManager.RawProjectBytes);
            
        // ----------
        // Save is done! now reload it.
        // ----------

        projectFileManager.AfterDeserialize += (serializer, root) =>
        {
            root.SaveVersion.Should().Be(saveVersionToUse, "It was saved with the older file format");
                
            // here's the bug: the deserialized data is wrong and needs to be fixed.
            // this is invoked before the post-serialize migrations run, so will still be wrong.
            // outside this callback, it should get fixed up automatically by the migration code.
            if (!string.IsNullOrEmpty(badGameName))
                root.Project.InternalRomGameName.Should().Be(badGameName, "Migrations to fix the bug haven't run yet.");

            root.Project.Data.RomBytes.Count.Should().BePositive("Expected bytes to be read by this point");
            root.Project.Data.RomBytesLoaded.Should()
                .BeFalse("Our mock has loaded bytes but not fixed this setting yet");
            
            // now we make sure the loader doesn't attempt to scan for compatible ROMs
            root.Project.Data.RomBytesLoaded = true;
        };

        Project? deserializedProject = null;
        var runDeserializer = new Action(() =>
        {
            deserializedProject = projectFileManager.Open("IGNORED").Root.Project;
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
            deserializedProject.Should().NotBeNull("it should have deserialized correctly.");
            if (deserializedProject == null) 
                return;
            
            deserializedProject.InternalRomGameName.Should().NotBe(badGameName,
                "Migrations should have fixed this");
            deserializedProject.InternalRomGameName.Should().Be(originalGoodGameName,
                "the automatic fix should have set this name back to the correct one");
        }
    }
}