using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Core.model.snes;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Cpu._65816;
using Diz.Cpu._65816.import;
using Diz.Test.Utils;
using FluentAssertions;
using LightInject;
using Moq;
using Xunit;

namespace Diz.Test.Tests.SerializationTests;

/// <summary>
/// Tests for the save-format 106 -> 107 migration (docs/diz/regions-as-partition-plan.md
/// §A.5): it must be a real, data-creating migration (not a MigrationNoOp), purely additive,
/// and idempotent against a project that already has an equivalent hand-authored region (the
/// CT "BankC0 - location" case).
/// </summary>
public class MigrationAddBankRegions107Tests : ContainerFixture
{
    [Inject] private readonly ISnesSampleProjectFactory sampleProjectFactory = null!;

    private static Mock<IAddRomDataCommand> MockCommandFor(Project project)
    {
        var mock = new Mock<IAddRomDataCommand>();
        mock.SetupGet(x => x.Root).Returns(new ProjectXmlSerializer.Root { Project = project });
        return mock;
    }

    [Fact]
    public void AppliesToSaveVersion106()
    {
        new MigrationAddBankRegions107().AppliesToSaveVersion.Should().Be(106);
    }

    [Fact]
    public void FreshProjectGetsOneBankRegionPerBank()
    {
        // sample ROM is 0x8000 bytes, LoRom -> exactly 1 bank ($80, per SampleRomData's own
        // DataBank=0x80 annotations), no regions defined yet.
        var project = (Project)sampleProjectFactory.Create();
        project.Data.Regions.Should().BeEmpty();

        new MigrationAddBankRegions107().OnLoadingAfterAddLinkedRom(MockCommandFor(project).Object);

        project.Data.Regions.Should().ContainSingle();
        var region = project.Data.Regions[0];
        region.RegionName.Should().Be("bank_80");
        region.StartSnesAddress.Should().Be(0x800000);
        region.EndSnesAddress.Should().Be(0x80FFFF); // inclusive, last byte IN the bank (§A.2.2)
        region.ExportSeparateFile.Should().BeTrue();
        region.Priority.Should().Be(0);
        region.IsFileProducingRegion().Should().BeTrue();
    }

    [Fact]
    public void ExistingHandAuthoredRegionOfTheSameExtentIsNotDuplicated()
    {
        // mirrors CT's "BankC0 - location": a user region with the exact same extent as the
        // bank that would otherwise be synthesized.
        var project = (Project)sampleProjectFactory.Create();
        project.Data.Regions.Add(new Region
        {
            RegionName = "Bank80 - hand authored",
            StartSnesAddress = 0x800000,
            EndSnesAddress = 0x80FFFF,
            ExportSeparateFile = true,
        });

        new MigrationAddBankRegions107().OnLoadingAfterAddLinkedRom(MockCommandFor(project).Object);

        // still just the one, hand-authored, untouched region -- migration must not duplicate it
        project.Data.Regions.Should().ContainSingle();
        project.Data.Regions[0].RegionName.Should().Be("Bank80 - hand authored");
    }

    [Fact]
    public void MigrationIsIdempotentWhenRunTwice()
    {
        var project = (Project)sampleProjectFactory.Create();
        var migration = new MigrationAddBankRegions107();

        migration.OnLoadingAfterAddLinkedRom(MockCommandFor(project).Object);
        project.Data.Regions.Should().ContainSingle();

        // running it again (e.g. a project that somehow gets the migration re-applied) must not
        // add a second copy
        migration.OnLoadingAfterAddLinkedRom(MockCommandFor(project).Object);
        project.Data.Regions.Should().ContainSingle();
    }

    [Fact]
    public void DoesNotTouchEndSnesAddressOfExistingRegions()
    {
        // per §A.2.2, this migration must NEVER convert EndSnesAddress on anything that
        // already exists -- that was a one-time hand-edit of the project XML, done outside
        // this migration entirely.
        var project = (Project)sampleProjectFactory.Create();
        project.Data.Regions.Add(new Region
        {
            RegionName = "some_asset",
            StartSnesAddress = 0x800100,
            EndSnesAddress = 0x8001FF, // whatever convention it already uses
            ExportSeparateFile = false,
        });

        new MigrationAddBankRegions107().OnLoadingAfterAddLinkedRom(MockCommandFor(project).Object);

        var untouched = project.Data.Regions[0];
        untouched.RegionName.Should().Be("some_asset");
        untouched.StartSnesAddress.Should().Be(0x800100);
        untouched.EndSnesAddress.Should().Be(0x8001FF);
    }
}
