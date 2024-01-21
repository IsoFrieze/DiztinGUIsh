#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.Cpu._65816.import;
using Diz.Test.Utils;
using FluentAssertions;
using IX.StandardExtensions.Extensions;
using LightInject;
using Moq;
using Xunit;
using IFileByteProvider = Diz.Core.serialization.IFileByteProvider;

namespace Diz.Test.bugs;

public class Bug50ProjectFileManager : ProjectFileManager
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
        Func<IAddRomDataCommand> addRomDataCommandCreate,
        Func<string, IFileByteProvider> fileByteProviderFactory) 
        : base(
            projectXmlSerializerCreate, 
            addRomDataCommandCreate,
            fileByteProviderFactory)
    {
    }
}

// https://github.com/Dotsarecool/DiztinGUIsh/issues/50
public class Bug050JapaneseText
{
    public class Bug050Fixture : ContainerFixture
    {
        // these will be populated by dependency injection
        [Inject] private readonly ISnesSampleProjectFactory sampleProjectFactory = null!;
        [Inject] private readonly Func<IProjectXmlSerializer> fnProjectSerializerCreate = null!;
        [Inject] private readonly Func<IAddRomDataCommand> fnAddRomDataCommand = null!;
        
        // generated
        internal readonly bool ExpectedCartTitleValidationException;
        internal readonly Project ProjectToSerialize;
        internal readonly string OriginalGoodGameName;
        internal readonly string? BadGameName = null;
        internal readonly int SaveVersionToUse;
        internal readonly Bug50ProjectFileManager ProjectFileManager;
        internal readonly FileIoFixture FileIo;

        // populated just before serialization
        internal string? BeforeDeserializeInternalRomGameName;
        internal string? BeforeSerializeCartTitleName;
        
        // populated after deserialization
        internal int DeserializedSaveVersionBeforeMigrations = -1;
        internal string? DeserializedInternalRomGameNameBeforeMigrations;
        
        internal Project? DeserializedProject = null;
        internal ProjectOpenResult? ProjectDeserializedResult = null;

        public Bug050Fixture(
            bool forceOlderVersion100,
            string? overrideGameName,
            bool expectedCartTitleValidationException) : 
            base(injectFieldsOnlyIfNull: true, injectOnlyTaggedFields: true)
        {
            FileIo = new FileIoFixture();

            ProjectFileManager = new Bug50ProjectFileManager(
                fnProjectSerializerCreate, 
                fnAddRomDataCommand, 
                _ => FileIo.Mock.Object
            );
            
            ExpectedCartTitleValidationException = expectedCartTitleValidationException;
            SaveVersionToUse = forceOlderVersion100 ? 100 : 101;
            
            ProjectToSerialize = (sampleProjectFactory.Create() as Project)!;
            OriginalGoodGameName = ProjectToSerialize.InternalRomGameName;
            BadGameName = overrideGameName == null ? null : 
                ByteUtil.ReadShiftJisEncodedString(
                    ByteUtil.PadCartridgeTitleBytes(
                        ByteUtil.ConvertUtf8ToShiftJisEncodedBytes(overrideGameName)
                    )
                );

            // TODO
            // if (ExpectedCartTitleValidationException)
            // {
            //     ProjectFileManager.RomPromptFn = _ =>
            //         throw new InvalidDataException("We should not hit this for this test, if it worked.");
            // }

            ProjectFileManager.BeforeSerialize += (serializer, rootElement) =>
            {
                // force the save data we're about use to be a specific version, mitigations will only run at v100 and that's most of what we're testing
                rootElement.SaveVersion = SaveVersionToUse;

                // if requested, intentionally break the cached cart name data by setting it to an invalid value.
                // this will be serialized, and on deserialized, the migration code should detect this as a previous serialization bug and fix it.
                if (!string.IsNullOrEmpty(BadGameName))
                    rootElement.Project.InternalRomGameName = BadGameName;

                // store these for testing later
                var snesApi = rootElement.Project.Data.GetSnesApi();
                BeforeDeserializeInternalRomGameName = rootElement.Project.InternalRomGameName;
                BeforeSerializeCartTitleName = snesApi?.CartridgeTitleName;
            };
            
            ProjectFileManager.AfterDeserialize += (serializer, root) =>
            {
                // save some info for later
                DeserializedSaveVersionBeforeMigrations = root.SaveVersion;
                DeserializedInternalRomGameNameBeforeMigrations = root.Project.InternalRomGameName;
            };
            
            GetInstance<IReadFromFileBytes>().Should().Match(o => o.GetType().Name.Contains("Proxy"));

            // SUT
            ProjectFileManager.Save(ProjectToSerialize, "IGNORED");
            var runDeserializerFn = () => ProjectFileManager.Open("IGNORED");
            var functionAssertions = runDeserializerFn.Invoking(a => ProjectDeserializedResult = a());
            if (ExpectedCartTitleValidationException)
            {
                // the original bug would trigger an exception on failed verification. make sure that is still happening
                functionAssertions.Should().Throw<InvalidOperationException>().WithMessage("Search failed, *");
                return;
            }

            // this is what it looks like when either the bug is is fixed, or things are normal
            functionAssertions.Should()
                .NotThrow("Didn't expect this to throw because serialized data shouldn't be broken");
                
            DeserializedProject = ProjectDeserializedResult?.Root?.Project;
            Assert.True(DeserializedProject != null);
        }
        
        protected override void Configure(IServiceRegistry serviceRegistry)
        {
            base.Configure(serviceRegistry);

            serviceRegistry.Register<IReadFromFileBytes>(factory =>
            {
                var mockedFileBytes = ProjectToSerialize.Data.GetFileBytes().ToArray();
                var mockLinkedRomBytesProvider = TestUtil.CreateReadFromFileMock(mockedFileBytes);
                return mockLinkedRomBytesProvider.Object;
            });
        }

        public override IServiceContainer ConfigureAndRegisterServiceContainer()
        {
            var container = base.ConfigureAndRegisterServiceContainer();
            
            // normally we'd do all this in Configure().
            // but if we do it here instead, we can override the regular system-level assignments.
            
            // overwrite the existing IProjectXmlSerializer instantiation registration so we can do some funny business. 
            container.Register<IProjectXmlSerializer>(factory =>
            {
                const int overriddenTargetSaveVersionForTests = 101;
                
                return new ProjectXmlSerializer(
                    xmlSerializerFactory: factory.GetInstance<IXmlSerializerFactory>(),
                    migrationRunner: factory.GetInstance<IMigrationRunner>(),
                    
                    // this creates a testing version of our migration runner that only tried to upgrade 
                    // from v100 to v101, and will not run anything else.
                    migrateLoadedXmlToVersion: overriddenTargetSaveVersionForTests
                    );
            });
            
            return container;
        }
    }

    // scenarios:
    // A) we have a v100 save file, which means:
    // - if the Cart Name in the xml matches the ROM, we're ok
    // - if the Cart names don't match, but the checksums do, then it's likely we hit the v100 serializer bug.
    //   in that case, the v100 mitigation should detect this and re-cache the cart name on load, upgrading it to v101.
    //
    // B) we have a > v100 save file which can ONLY mean:
    // - if the Cart name OR the checksum in the XML don't match the rom, then we should reject the ROM.
    public static TheoryData<Bug050Fixture> Fixtures =>
        new()
        {
            // nothing wrong, no migrations needed, everything should work like normal.
            new Bug050Fixture(forceOlderVersion100: true, overrideGameName: null, expectedCartTitleValidationException: false),
            //
            // older save file format, has a messed up cached name saved in project XML, our migrations should detect and run, fixing it.
            // then when validation code runs, it shouldn't throw an exception
            new Bug050Fixture(forceOlderVersion100: true, overrideGameName: "BUGGY_NAME", expectedCartTitleValidationException: false),
            //
            // newest save format. since there's no more buggy data caching, if the names don't match, then we KNOW
            // there's a legit issue with the selected linked ROM file and we SHOULD rightly throw an exception 
            new Bug050Fixture(forceOlderVersion100: false, overrideGameName: "LEGIT_PROB", expectedCartTitleValidationException: true),
        };

    [Theory, MemberData(nameof(Fixtures))]
    public void ExpectValidSerializationInput(Bug050Fixture fixture)
    {
        fixture.ProjectToSerialize.Should().NotBeNull();
        fixture.ProjectToSerialize.Data.RomBytes[0].Rom.Should().Be(0x78);
        fixture.FileIo.FakeFileBytes
            .Should().NotBeNullOrEmpty("Expected some file bytes to be written by serialization process");
    }

    [Theory, MemberData(nameof(Fixtures))]
    public void ExpectedSetupOutputs(Bug050Fixture fixture)
    {
        if (fixture.ExpectedCartTitleValidationException)
            return;
        
        fixture.DeserializedProject.Should().NotBeNull();
        fixture.DeserializedProject!.Data.RomBytes[0].Rom.Should().Be(0x78);
    }

    [Theory, MemberData(nameof(Fixtures))]
    public void ExpectSaveVersionsToMatch(Bug050Fixture fixture)
    {
        fixture.DeserializedSaveVersionBeforeMigrations.Should()
            .Be(fixture.SaveVersionToUse, "It was saved with the older file format");
    }

    [Theory, MemberData(nameof(Fixtures))]
    public void TestCartTitleOverridesApplied(Bug050Fixture fixture)
    {
        // if we're not using an override to intentionally create a buggy situation, no need to run anything else here
        if (string.IsNullOrEmpty(fixture.BadGameName)) 
            return;
        
        Assert.NotEqual(
            fixture.BeforeDeserializeInternalRomGameName,
            fixture.BeforeSerializeCartTitleName);

        // here's the bug: the deserialized data is wrong and needs to be fixed.
        // this is invoked before the post-serialize migrations run, so will still be wrong.
        // outside this callback, it should get fixed up automatically by the migration code.
        if (!string.IsNullOrEmpty(fixture.BadGameName))
            fixture.DeserializedInternalRomGameNameBeforeMigrations.Should().Be(
                fixture.BadGameName,
                "Cart title used in test before migrations ran should result in using a bad game name here");
    }
    
    [Theory, MemberData(nameof(Fixtures))]
    public void TestExceptionFiredWhenExpectedForNonMatchingData(Bug050Fixture fixture)
    {
        if (fixture.ExpectedCartTitleValidationException) 
            return;
        
        fixture.DeserializedProject.Should().NotBeNull("it should have deserialized correctly.");
        if (fixture.DeserializedProject == null)
            return;

        var deserializedGameName = fixture.DeserializedProject.InternalRomGameName;
        deserializedGameName.Should()
            .NotBe(fixture.BadGameName, "Migrations should have fixed this");

        deserializedGameName.Should()
            .Be(fixture.OriginalGoodGameName,
                "the automatic fix should have set this name back to the correct one");
    }
}