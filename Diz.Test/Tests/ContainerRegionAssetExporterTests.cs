using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using Diz.LogWriter.assets;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests;

/// <summary>
/// Tests for exporting a region that packs several assets into one span.
///
/// The theme running through them: a container's own bytes are all Diz can see. The buffer its
/// members tile only exists after the build has unpacked it, so the decomposition is authored,
/// authoring is checked hard here, and the claims that cannot be checked here (does the hash
/// actually match? does the tiling actually cover the buffer?) are checked by the build against
/// the real bytes. Nothing is assumed at either end.
/// </summary>
public class ContainerRegionAssetExporterTests : IDisposable
{
    private readonly string tempDir;

    public ContainerRegionAssetExporterTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "diz-container-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
    }

    // A plausible-looking hash per member; only its shape is checkable at export time.
    private const string ShaA = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string ShaB = "2222222222222222222222222222222222222222222222222222222222222222";

    private class FakeByteSource : IReadOnlyByteSource
    {
        public byte? GetRomByte(int offset) => offset is >= 0 and < 0x1000 ? (byte)(offset * 7 + 13) : null;
        public int? GetRomWord(int offset) => null;
        public int? GetRomLong(int offset) => null;
        public int? GetRomDoubleWord(int offset) => null;
    }

    private class FakeAddressConverter : ISnesAddressConverter
    {
        private const int Base = 0xC00000;
        public int ConvertSnesToPc(int offset) => offset >= Base ? offset - Base : -1;
        public int ConvertPCtoSnes(int offset) => offset + Base;
    }

    private static RegionAssetExportService MakeService()
    {
        IRegionAssetExporter[] leaves =
        [
            new BinaryRegionAssetExporter(), new GfxRegionAssetExporter(),
            new BrrRegionAssetExporter(), new TextRegionAssetExporter(),
        ];

        return new RegionAssetExportService(new FakeByteSource(), new FakeAddressConverter(),
            [..leaves, new ContainerRegionAssetExporter(leaves)]);
    }

    private static Region ContainerRegion(string assetOptions, int length = 64) => new()
    {
        RegionName = "blob/pack",
        AssetName = "blob/pack",
        StartSnesAddress = 0xC00200,
        EndSnesAddress = 0xC00200 + length - 1,
        ExportType = RegionExportType.Asset,
        AssetType = "blob.container",
        AssetOptions = assetOptions,
    };

    private JsonElement ReadManifest(params string[] pathParts) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine([tempDir, .. pathParts]))).RootElement;

    /// <summary>Two gfx members tiling a 96-byte buffer, behind a compression stage.</summary>
    private const string TwoGfxMembers = """
        {
          "lz": { "mode": 12 },
          "members": [
            { "name": "gfx/a", "at": 0,  "len": 64, "type": "gfx.snes.2bpp",
              "sha256": "1111111111111111111111111111111111111111111111111111111111111111" },
            { "name": "gfx/b", "at": 64, "len": 32, "type": "gfx.snes.2bpp",
              "sha256": "2222222222222222222222222222222222222222222222222222222222222222" }
          ]
        }
        """;

    // ---- the container manifest -------------------------------------------------------------

    [Fact]
    public void TheContainerManifestCarriesAPipelineAndMemberReferences()
    {
        MakeService().ExportRegion(ContainerRegion(TwoGfxMembers), tempDir);

        var manifest = ReadManifest("blob", "pack.json");

        // key order is load-bearing; a container slots pipeline + members where a leaf puts its
        // typed block, and carries no typed block of its own.
        manifest.EnumerateObject().Select(p => p.Name).Should()
            .Equal("name", "type", "source", "pipeline", "members", "generated_by");

        var stage = manifest.GetProperty("pipeline")[0];
        stage.GetProperty("codec").GetString().Should().Be("compress.ct.lzss");
        stage.GetProperty("lz").GetProperty("mode").GetInt32().Should().Be(12);

        // the container's own source is the span AS STORED -- the compressed bytes, at a real ROM
        // offset. The decompressed length is nowhere in it, because nothing needs it: the build
        // measures the buffer it actually produced.
        manifest.GetProperty("source").GetProperty("length").GetInt32().Should().Be(64);
        manifest.GetProperty("source").GetProperty("rom_offset").GetString().Should().Be("0x200");
    }

    [Fact]
    public void MembersAreReferencesOnlyNotInlineDescriptions()
    {
        // Everything about how a member is decoded lives in the member's own manifest. Inlining
        // it here would mean a mod overriding one member had to own a copy of every sibling's
        // geometry too, and the two copies would be free to drift.
        MakeService().ExportRegion(ContainerRegion(TwoGfxMembers), tempDir);

        var members = ReadManifest("blob", "pack.json").GetProperty("members");

        members.GetArrayLength().Should().Be(2);
        members[0].EnumerateObject().Select(p => p.Name).Should().Equal("name", "at", "len");
        members[0].GetProperty("name").GetString().Should().Be("gfx/a");
        members[1].GetProperty("at").GetInt32().Should().Be(64);
    }

    // ---- the member manifests ---------------------------------------------------------------

    [Fact]
    public void EachMemberGetsAnOrdinaryLeafManifestOfItsOwn()
    {
        MakeService().ExportRegion(ContainerRegion(TwoGfxMembers), tempDir);

        var member = ReadManifest("gfx", "a.json");

        // identical in shape to a top-level gfx asset -- that is what lets fork, mod overrides
        // and verify work on a member with no new machinery at all.
        member.EnumerateObject().Select(p => p.Name).Should()
            .Equal("name", "type", "source", "gfx", "generated_by");
        member.GetProperty("type").GetString().Should().Be("gfx.snes.2bpp");

        // the geometry is derived from the member's declared length, not from bytes Diz holds
        member.GetProperty("gfx").GetProperty("tiles").GetInt32().Should().Be(64 / 16);
    }

    [Fact]
    public void AMemberRecordsWhereItSitsInTheContainerAndNoRomOffset()
    {
        MakeService().ExportRegion(ContainerRegion(TwoGfxMembers), tempDir);

        var source = ReadManifest("gfx", "b.json").GetProperty("source");

        source.EnumerateObject().Select(p => p.Name).Should()
            .Equal("length", "source_sha256", "member_of", "at");
        source.GetProperty("member_of").GetString().Should().Be("blob/pack");
        source.GetProperty("at").GetInt32().Should().Be(64);
        source.GetProperty("length").GetInt32().Should().Be(32);
        source.GetProperty("source_sha256").GetString().Should().Be(ShaB);

        // a member has no offset in the cartridge: its bytes do not exist until the container is
        // unpacked. Emitting one would invite a comparison against the compressed ROM bytes.
        source.TryGetProperty("rom_offset", out _).Should().BeFalse();
        source.TryGetProperty("snes_addr", out _).Should().BeFalse();
    }

    [Fact]
    public void MemberOptionsReachTheMembersOwnCodecBlock()
    {
        // A member is authored exactly like a standalone region of its type, options included --
        // the container passes them straight to the exporter that owns that type.
        var options = """
            {
              "members": [
                { "name": "text/credits", "at": 0, "len": 22, "type": "text.ct.mapped",
                  "sha256": "1111111111111111111111111111111111111111111111111111111111111111",
                  "options": { "tbl": "text/ct_8px.tbl", "record_width": 11, "pad": "0xEF" } }
              ]
            }
            """;

        var result = MakeService().ExportRegion(ContainerRegion(options, 22), tempDir);

        var text = ReadManifest("text", "credits.json").GetProperty("text");
        text.GetProperty("tbl").GetString().Should().Be("text/ct_8px.tbl");
        text.GetProperty("count").GetInt32().Should().Be(2);

        // and the shared file it names travels with the member's build node, so the table becomes
        // a dependency of the member's decode edge just as it would at the top level.
        result.BuildNodes.Single().Members.Single().SharedFiles.Should().Equal("text/ct_8px.tbl");
    }

    // ---- the build graph --------------------------------------------------------------------

    [Fact]
    public void TheContainerNodeCarriesItsStageAndItsMembers()
    {
        var result = MakeService().ExportRegion(ContainerRegion(TwoGfxMembers), tempDir);

        var node = result.BuildNodes.Should().ContainSingle().Subject;
        node.Name.Should().Be("blob/pack");
        node.Pipeline.Should().ContainSingle().Which.Codec.Should().Be("compress.ct.lzss");
        node.Members.Select(m => m.Name).Should().Equal("gfx/a", "gfx/b");
        node.Members.Select(m => m.AssetType).Should().AllBe("gfx.snes.2bpp");

        // exactly one directive: a region occupies one span, whatever is packed inside it.
        result.AsmDirective.Should().Be("incbin \"build/assets/blob/pack.bin\"");
    }

    [Fact]
    public void MembersDoNotAppearAsTopLevelAssets()
    {
        // A member has no ROM offset, so it must never be handed to the build as something to
        // extract from the ROM. It reaches the graph only through its container.
        var service = MakeService();
        service.ExportRegion(ContainerRegion(TwoGfxMembers), tempDir);

        service.ExportedBuildNodes.Select(n => n.Name).Should().Equal("blob/pack");
    }

    [Fact]
    public void AContainerWithNoTransformDeclaresNoPipeline()
    {
        var options = """
            {
              "members": [
                { "name": "gfx/a", "at": 0, "len": 64, "type": "gfx.snes.2bpp",
                  "sha256": "1111111111111111111111111111111111111111111111111111111111111111" }
              ]
            }
            """;

        var result = MakeService().ExportRegion(ContainerRegion(options), tempDir);

        result.BuildNodes.Single().Pipeline.Should().BeEmpty();
        ReadManifest("blob", "pack.json").TryGetProperty("pipeline", out _).Should()
            .BeFalse("a key that says nothing still churns every tracked manifest");
    }

    // ---- authoring that must fail loudly ----------------------------------------------------

    private void ExportShouldFailWith(string options, string messagePattern) =>
        FluentActions.Invoking(() => MakeService().ExportRegion(ContainerRegion(options), tempDir))
            .Should().Throw<InvalidOperationException>().WithMessage(messagePattern);

    [Fact]
    public void MembersLeavingAHoleAreRejected()
    {
        // Unclaimed bytes are bytes the build would silently drop on the way back in.
        ExportShouldFailWith("""
            {
              "members": [
                { "name": "gfx/a", "at": 0,  "len": 32, "type": "gfx.snes.2bpp",
                  "sha256": "1111111111111111111111111111111111111111111111111111111111111111" },
                { "name": "gfx/b", "at": 48, "len": 16, "type": "gfx.snes.2bpp",
                  "sha256": "2222222222222222222222222222222222222222222222222222222222222222" }
              ]
            }
            """, "*HOLE*");
    }

    [Fact]
    public void OverlappingMembersAreRejected()
    {
        ExportShouldFailWith("""
            {
              "members": [
                { "name": "gfx/a", "at": 0,  "len": 48, "type": "gfx.snes.2bpp",
                  "sha256": "1111111111111111111111111111111111111111111111111111111111111111" },
                { "name": "gfx/b", "at": 32, "len": 16, "type": "gfx.snes.2bpp",
                  "sha256": "2222222222222222222222222222222222222222222222222222222222222222" }
              ]
            }
            """, "*OVERLAPS*");
    }

    [Fact]
    public void MembersDeclaredOutOfAddressOrderAreRejected()
    {
        // Legal-looking and quietly fatal: reassembly concatenates in declaration order, so a
        // permutation rebuilds the buffer scrambled and passes every length check downstream.
        ExportShouldFailWith("""
            {
              "members": [
                { "name": "gfx/b", "at": 32, "len": 32, "type": "gfx.snes.2bpp",
                  "sha256": "2222222222222222222222222222222222222222222222222222222222222222" },
                { "name": "gfx/a", "at": 0,  "len": 32, "type": "gfx.snes.2bpp",
                  "sha256": "1111111111111111111111111111111111111111111111111111111111111111" }
              ]
            }
            """, "*ascending order*");
    }

    [Fact]
    public void AMemberWithNoHashIsRejected()
    {
        // The hash is the whole provenance claim for a member: without it the manifest is
        // indistinguishable from a hand-authored asset, which is the one class of asset the
        // codecs decode without checking anything.
        ExportShouldFailWith("""
            {
              "members": [
                { "name": "gfx/a", "at": 0, "len": 64, "type": "gfx.snes.2bpp" }
              ]
            }
            """, "*sha256*");
    }

    [Fact]
    public void AMalformedHashIsRejected()
    {
        ExportShouldFailWith("""
            {
              "members": [
                { "name": "gfx/a", "at": 0, "len": 64, "type": "gfx.snes.2bpp", "sha256": "DEADBEEF" }
              ]
            }
            """, "*64 lowercase hex*");
    }

    [Fact]
    public void AMemberWithNoTypeIsRejected()
    {
        // Nothing about the bytes says what they are, and a container that guessed would produce
        // a member manifest describing data nobody authored.
        ExportShouldFailWith("""
            {
              "members": [
                { "name": "gfx/a", "at": 0, "len": 64,
                  "sha256": "1111111111111111111111111111111111111111111111111111111111111111" }
              ]
            }
            """, "*\"type\"*");
    }

    [Fact]
    public void ANestedContainerMemberIsRejected()
    {
        // The grammar is recursive but only one level is built; a nested container would write a
        // manifest that nothing in the build graph knows how to unpack.
        ExportShouldFailWith("""
            {
              "members": [
                { "name": "blob/inner", "at": 0, "len": 64, "type": "blob.container",
                  "sha256": "1111111111111111111111111111111111111111111111111111111111111111" }
              ]
            }
            """, "*no exporter handles*");
    }

    [Fact]
    public void AnEmptyMemberListIsRejected()
    {
        ExportShouldFailWith("""{ "members": [] }""", "*at least one*");
    }

    [Fact]
    public void AContainerWithNoAuthoredDecompositionIsRejected()
    {
        FluentActions.Invoking(() => MakeService().ExportRegion(ContainerRegion(null), tempDir))
            .Should().Throw<InvalidOperationException>().WithMessage("*has no Asset Options*");
    }

    [Fact]
    public void AnUnknownTransformBlockIsRejectedNamingTheKnownOnes()
    {
        // A typo'd stage key would otherwise be silently ignored and the container would build
        // as though it were uncompressed.
        ExportShouldFailWith("""
            {
              "lzz": { "mode": 12 },
              "members": [
                { "name": "gfx/a", "at": 0, "len": 64, "type": "gfx.snes.2bpp",
                  "sha256": "1111111111111111111111111111111111111111111111111111111111111111" }
              ]
            }
            """, "*does not name a known transform stage*");
    }

    [Fact]
    public void DuplicateMemberNamesAreRejected()
    {
        ExportShouldFailWith("""
            {
              "members": [
                { "name": "gfx/a", "at": 0,  "len": 32, "type": "gfx.snes.2bpp",
                  "sha256": "1111111111111111111111111111111111111111111111111111111111111111" },
                { "name": "gfx/a", "at": 32, "len": 32, "type": "gfx.snes.2bpp",
                  "sha256": "2222222222222222222222222222222222222222222222222222222222222222" }
              ]
            }
            """, "*declared twice*");
    }

    [Fact]
    public void AMemberWhoseLengthItsCodecCannotRepresentIsRejected()
    {
        // The member's own exporter validates it exactly as it would a top-level region: 60 bytes
        // is not a whole number of 2bpp tiles, and a container must not be a way around that.
        ExportShouldFailWith("""
            {
              "members": [
                { "name": "gfx/a", "at": 0, "len": 60, "type": "gfx.snes.2bpp",
                  "sha256": "1111111111111111111111111111111111111111111111111111111111111111" }
              ]
            }
            """, "*whole number of 2bpp tiles*");
    }
}
