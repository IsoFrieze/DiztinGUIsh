using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Diz.LogWriter.assets;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests;

public class BuildFileGeneratorTests : IDisposable
{
    private readonly string tempDir;

    public BuildFileGeneratorTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "diz-build-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
    }

    /// <summary>
    /// A build node as an exporter would report it. The generator consumes nodes, not regions:
    /// what a region turned into is the exporter's answer, not something re-derived here.
    /// </summary>
    private static AssetBuildNode AssetNode(string name, string assetType = "gfx.snes.2bpp",
        params string[] sharedFiles) => new()
    {
        Name = name,
        AssetType = assetType,
        SharedFiles = sharedFiles ?? [],
    };

    /// <summary>
    /// A container node as the container exporter reports it: members tiling a buffer, optionally
    /// behind one transform stage.
    /// </summary>
    private static AssetBuildNode ContainerNode(string name, bool compressed,
        params AssetBuildNode[] members) => new()
    {
        Name = name,
        AssetType = "blob.container",
        Pipeline = compressed
            ?
            [
                new AssetPipelineStage
                {
                    Codec = "compress.ct.lzss",
                    BlockKey = "lz",
                    Block = new JsonObject { ["mode"] = 12 },
                },
            ]
            : [],
        Members = members,
    };

    /// <summary>Every `build` statement in a generated file, one per emitted edge.</summary>
    private static List<string> BuildEdges(string ninja) =>
        ninja.Split('\n').Select(l => l.TrimEnd('\r'))
            .Where(l => l.StartsWith("build ", StringComparison.Ordinal))
            .ToList();

    /// <summary>The three edges every project has regardless of assets: the ROM, verify, extract.</summary>
    private const int FixedEdges = 3;

    [Fact]
    public void BinaryRegionsWireThroughBinpackAsRawAssets()
    {
        var ninja = new BuildFileGenerator().Generate([AssetNode("data/raw", "raw.bin")]);

        ninja.Should().Contain("rule raw_compile");
        ninja.Should().Contain("rule raw_extract");
        ninja.Should().Contain("  command = python $binpack compile --name $name $search_roots --out $out");

        // the editable source and the compiled payload share an extension (a verbatim asset has
        // no lossy view), so they must be distinguished by tier, not by name.
        ninja.Should().Contain(
            "build extracted/data/raw.bin: raw_extract | generated/assets/data/raw.json $binpack $orig_rom");
        ninja.Should().Contain(
            "build build/assets/data/raw.bin: raw_compile extracted/data/raw.bin | generated/assets/data/raw.json $binpack");

        // and the ROM depends on it -- without this edge a binary region would never rebuild.
        ninja.Should().Contain("build $out_rom: assemble | build/assets/data/raw.bin");
    }

    [Fact]
    public void CodecSharedByTwoBindingsIsDeclaredExactlyOnce()
    {
        // binpack backs both the verbatim audio and raw types. Emitting its var line per binding
        // would put the same assignment in build.ninja twice.
        var ninja = new BuildFileGenerator().Generate([
            AssetNode("audio/song", "audio.snes.brr"), AssetNode("data/raw", "raw.bin"),
        ]);

        ninja.Split("binpack = tools/vendor/dizpack/binpack.py").Length.Should().Be(2);
    }

    [Fact]
    public void AssetsAreSortedSoOutputIsDeterministic()
    {
        // build.ninja is checked in, and nodes arrive in the order the regions were exported
        // (ROM order), so the generator must sort -- otherwise moving a region churns the diff.
        var ninja = new BuildFileGenerator().Generate([
            AssetNode("gfx/zebra"), AssetNode("gfx/apple"), AssetNode("gfx/mango"),
        ]);

        ninja.IndexOf("# gfx/apple", StringComparison.Ordinal).Should()
            .BeLessThan(ninja.IndexOf("# gfx/mango", StringComparison.Ordinal));
        ninja.IndexOf("# gfx/mango", StringComparison.Ordinal).Should()
            .BeLessThan(ninja.IndexOf("# gfx/zebra", StringComparison.Ordinal));
    }

    [Fact]
    public void GeneratedBuildWiresAssetsIntoTheRom()
    {
        var ninja = new BuildFileGenerator().Generate([AssetNode("gfx/font")]);

        // the ROM is decoded into an editable PNG, driven by the manifest Diz wrote...
        ninja.Should().Contain(
            "build extracted/gfx/font.png: gfx_extract | generated/assets/gfx/font.json $gfxpack $orig_rom");
        ninja.Should().Contain("  manifest = generated/assets/gfx/font.json");
        // ...and that PNG is what the codec compiles into the incbin'd payload.
        ninja.Should().Contain(
            "build build/assets/gfx/font.bin: gfx_compile extracted/gfx/font.png | generated/assets/gfx/font.json $gfxpack");

        // The ROM must depend on the compiled asset. Without this edge, editing a PNG would
        // recompile the .bin but never re-assemble, and the ROM would silently go stale.
        ninja.Should().Contain("build $out_rom: assemble | build/assets/gfx/font.bin");

        ninja.Should().Contain("build verify: sha_verify $out_rom");
        ninja.Should().Contain("default $out_rom");
    }

    [Fact]
    public void ExtractRulesDecodeTheRomThroughTheAssetsOwnManifest()
    {
        var ninja = new BuildFileGenerator().Generate([AssetNode("gfx/font")]);

        // Extraction is ROM ground truth: the manifest path is passed explicitly rather than
        // resolved through the layer search path, so a mod layer can never redirect what comes
        // out of the ROM. $search_roots still rides along for shared files (e.g. a .tbl).
        ninja.Should().Contain("rule gfx_extract");
        ninja.Should().Contain(
            "  command = python $gfxpack extract --manifest $manifest --rom $orig_rom --out $out $search_roots");

        // the seed rules and targets are gone entirely -- extraction is a per-build edge now,
        // not a one-time create-then-yours step.
        ninja.Should().NotContain("_seed");
        ninja.Should().NotContain("build seed");
    }

    [Fact]
    public void ExtractPhonyTargetAggregatesEveryEditableSource()
    {
        var ninja = new BuildFileGenerator().Generate([AssetNode("gfx/b"), AssetNode("gfx/a")]);

        // one name for "give me the editable sources", without assembling a ROM.
        ninja.Should().Contain("build extract: phony extracted/gfx/a.png extracted/gfx/b.png");
    }

    [Fact]
    public void TextAssetsWireThroughTextpack()
    {
        var ninja = new BuildFileGenerator().Generate([
            AssetNode("text/item_names", "text.ct.mapped", "text/ct_8px.tbl"),
        ]);

        // textpack runs off its OWN vendored tool var (like binpack), not the shared gfxpack one.
        ninja.Should().Contain("textpack = tools/vendor/dizpack/textpack.py");
        ninja.Should().Contain("rule text_compile");
        ninja.Should().Contain("rule text_extract");

        // the character table is an input the manifest only NAMES, so it must be an implicit
        // dep of the extract edge: editing the table has to re-decode the text, or the build
        // keeps serving text rendered with the old glyph map.
        ninja.Should().Contain(
            "build extracted/text/item_names.yaml: text_extract | generated/assets/text/item_names.json " +
            "$textpack $orig_rom assets/text/ct_8px.tbl");

        // the editable .yaml is the compile input; the manifest is a dependency.
        ninja.Should().Contain(
            "build build/assets/text/item_names.bin: text_compile extracted/text/item_names.yaml " +
            "| generated/assets/text/item_names.json $textpack");
        // the ROM must depend on the compiled asset, or editing text would never re-assemble.
        ninja.Should().Contain("build $out_rom: assemble | build/assets/text/item_names.bin");
    }

    [Fact]
    public void TierDirectoryNamesComeFromSettings()
    {
        // the tier names are per-project settings, not constants baked into Diz.
        var settings = new BuildFileGeneratorSettings
        {
            MainAsmPath = "asm/main.asm",
            AssetsDir = "art",
            ExtractedDir = "decoded",
            BuildDir = "out",
            ManifestDir = "asm/manifests",
        };

        var ninja = new BuildFileGenerator(settings).Generate([AssetNode("gfx/font")]);

        ninja.Should().Contain(
            "search_roots = $mod_roots --search art --base-manifests asm/manifests --base-content decoded");
        ninja.Should().Contain("build decoded/gfx/font.png: gfx_extract | asm/manifests/gfx/font.json");
        ninja.Should().Contain("build out/assets/gfx/font.bin: gfx_compile decoded/gfx/font.png");
    }

    [Fact]
    public void GeneratedBuildIncludesTheUserConfigAndDefinesNoGameSpecificVars()
    {
        var ninja = new BuildFileGenerator().Generate([]);

        ninja.Should().Contain("include build-config.ninja");

        // the game-specific values ($asar, $out_rom, $orig_rom, $mod_roots) are the user's,
        // defined in build-config.ninja. build.ninja must only reference them -- defining
        // them here would mean every re-export clobbers the user's settings.
        ninja.Should().NotContain("\nasar =");
        ninja.Should().NotContain("\nout_rom =");
        ninja.Should().NotContain("\norig_rom =");
        ninja.Should().NotContain("\nmod_roots =");

        // mod layers come first; the hand-authored assets/ layer is last. Below it the base
        // pair is split across the two generated tiers: manifests Diz writes, content the
        // build extracts.
        ninja.Should().Contain(
            "search_roots = $mod_roots --search assets --base-manifests generated/assets --base-content extracted");
    }

    [Fact]
    public void WriteToSeedsBuildConfigWhenMissing()
    {
        new BuildFileGenerator().WriteTo(tempDir, []);

        var configPath = Path.Combine(tempDir, "build-config.ninja");
        File.Exists(configPath).Should().BeTrue("first export must seed the user config");

        var config = File.ReadAllText(configPath);
        config.Should().Contain("asar = ");
        config.Should().Contain("out_rom = ");
        config.Should().Contain("orig_rom = ");
        config.Should().Contain("mod_roots =");
    }

    [Fact]
    public void WriteToNeverOverwritesAnExistingBuildConfig()
    {
        var configPath = Path.Combine(tempDir, "build-config.ninja");
        var userConfig = "# hand-edited\nasar = bin\\my-asar.exe\nout_rom = output/my.sfc\n" +
                         "orig_rom = roms/my-orig.sfc\nmod_roots = --search assets/mymod\n";
        File.WriteAllText(configPath, userConfig);
        var before = File.ReadAllBytes(configPath);

        // regenerate twice; build.ninja may churn freely, the config must not
        new BuildFileGenerator().WriteTo(tempDir, []);
        new BuildFileGenerator().WriteTo(tempDir, [AssetNode("gfx/font")]);

        File.ReadAllBytes(configPath).Should().Equal(before,
            "build-config.ninja is user-owned; re-export must never touch it");
    }

    [Fact]
    public void GeneratedBuildIsValidWithNoAssets()
    {
        // A project with no asset regions must still produce a usable build file, not a
        // half-written one with dangling dependencies.
        var ninja = new BuildFileGenerator().Generate([]);

        ninja.Should().Contain("build $out_rom: assemble");
        ninja.Should().NotContain("gfx_compile extracted/");
        ninja.Should().Contain("build extract: phony");
    }

    [Fact]
    public void GeneratedBuildIsStableAcrossRuns()
    {
        AssetBuildNode[] assets = [AssetNode("gfx/a"), AssetNode("gfx/b")];
        var first = new BuildFileGenerator().Generate(assets);
        var second = new BuildFileGenerator().Generate(assets);

        second.Should().Be(first, "regenerating must produce a clean diff, not churn");
    }

    [Fact]
    public void WriteToProducesBuildNinjaAtExportRoot()
    {
        new BuildFileGenerator().WriteTo(tempDir, [AssetNode("gfx/f")]);

        File.Exists(Path.Combine(tempDir, "build.ninja")).Should().BeTrue();
    }

    [Fact]
    public void VendoringCopiesToolsAndWrapperIntoTheExport()
    {
        // Simulate a Diz install layout: <root>/tools/dizpack/{gfxpack.py,requirements.txt}
        var fakeDizRoot = Path.Combine(tempDir, "dizroot");
        var fakeTools = Path.Combine(fakeDizRoot, "tools", "dizpack");
        Directory.CreateDirectory(fakeTools);
        File.WriteAllText(Path.Combine(fakeTools, "gfxpack.py"), "# stub\n");
        File.WriteAllText(Path.Combine(fakeTools, "requirements.txt"), "Pillow>=10\n");

        var exportRoot = Path.Combine(tempDir, "export");
        Directory.CreateDirectory(exportRoot);

        var vendoring = new ToolVendoring();
        var written = vendoring.VendorInto(exportRoot, fakeTools);
        vendoring.WriteWrapper(exportRoot);

        written.Should().NotBeEmpty();
        File.Exists(Path.Combine(exportRoot, "tools", "vendor", "dizpack", "gfxpack.py")).Should().BeTrue();
        // requirements.txt must ship too, or the doc'd `pip install -r` has nothing to install
        File.Exists(Path.Combine(exportRoot, "tools", "vendor", "dizpack", "requirements.txt")).Should().BeTrue();
        File.Exists(Path.Combine(exportRoot, "build-assets.sh")).Should().BeTrue();

        // The wrapper must refuse `verify` when build-config.ninja sets mod_roots --
        // comparing a modded build against the original ROM is a guaranteed false failure.
        var wrapper = File.ReadAllText(Path.Combine(exportRoot, "build-assets.sh"));
        wrapper.Should().Contain("grep -q '^mod_roots = .' build-config.ninja");
        wrapper.Should().Contain("cannot be used");
        wrapper.Should().NotContain("MODS", "mod layers are configured in build-config.ninja, not an env var");
    }

    [Fact]
    public void VendoringWritesTheBuildingDocAtTheExportRoot()
    {
        var exportRoot = Path.Combine(tempDir, "doc-export");
        Directory.CreateDirectory(exportRoot);

        new ToolVendoring().WriteBuildingDoc(exportRoot);

        var docPath = Path.Combine(exportRoot, "BUILDING.md");
        File.Exists(docPath).Should().BeTrue("casual users need a quick-start at the repo root");

        var doc = File.ReadAllText(docPath);
        // generated-file treatment, same as build-assets.sh: rewritten every export
        doc.Should().Contain("GENERATED BY DiztinGUIsh -- DO NOT EDIT");
        // setup must point at the vendored requirements, or Pillow/ninja never get installed
        doc.Should().Contain("pip install -r tools/vendor/dizpack/requirements.txt");
        doc.Should().Contain("./build-assets.sh");
        doc.Should().Contain("ninja verify");
        doc.Should().Contain("ninja extract");
        // it must say where the editable files actually appear, and it must not still be
        // pointing readers at the old hand-authored location for them.
        doc.Should().Contain("extracted/");
        doc.Should().NotContain("assets/src");
    }

    [Fact]
    public void VendoringFindsToolsByWalkingUpFromABinDirectory()
    {
        // Running from bin/Debug/net10.0-windows/ must still locate tools/dizpack.
        var root = Path.Combine(tempDir, "walkup");
        var tools = Path.Combine(root, "tools", "dizpack");
        Directory.CreateDirectory(tools);
        File.WriteAllText(Path.Combine(tools, "gfxpack.py"), "# stub\n");
        File.WriteAllText(Path.Combine(tools, "requirements.txt"), "Pillow>=10\n");

        var deep = Path.Combine(root, "bin", "Debug", "net10.0-windows");
        Directory.CreateDirectory(deep);

        ToolVendoring.FindSourceToolsDir(deep).Should().Be(tools);
    }

    [Fact]
    public void VendoringReturnsEmptyWhenToolsAreMissing()
    {
        // Missing tools must degrade gracefully, not throw mid-export and leave a
        // half-written export behind.
        var exportRoot = Path.Combine(tempDir, "no-tools");
        Directory.CreateDirectory(exportRoot);

        var missing = Path.Combine(tempDir, "does-not-exist");
        new ToolVendoring().VendorInto(exportRoot, missing).Should().BeEmpty();
    }

    // ---- generalized codec dispatch --------------------------------------------------------

    [Fact]
    public void UnknownAssetTypeFailsLoudlyNamingTheType()
    {
        // Consistent with the exporter: an Asset region whose type no binding claims must
        // error loudly (naming the type), not be silently routed to the gfx codec as before.
        // (audio.* is now a registered default binding, so use a type that still has no binding
        // -- palette.snes.bgr555 is reserved in the taxonomy but not yet wired.)
        AssetBuildNode[] assets = [AssetNode("palette/pal0", "palette.snes.bgr555")];

        var act = () => new BuildFileGenerator().Generate(assets);

        act.Should().Throw<InvalidOperationException>().WithMessage("*palette.snes.bgr555*");
    }

    [Fact]
    public void AudioBrrIsAWiredDefaultBindingCompilingViaBinpack()
    {
        // The real audio binding ships in DefaultToolBindings, so a plain default
        // generator (what the exporter uses) wires BRR assets through binpack -- no test-only
        // binding needed. This is the non-synthetic counterpart to
        // PerAssetEdgesDispatchByTypePrefixToTheMatchingBinding below.
        var ninja = new BuildFileGenerator().Generate([
            AssetNode("audio/AudioBRR_00", "audio.snes.brr"),
        ]);

        // binpack runs off its own vendored var, declared as a `= ...` line...
        ninja.Should().Contain("binpack = tools/vendor/dizpack/binpack.py");
        ninja.Should().Contain("rule audio_compile");
        ninja.Should().Contain("rule audio_extract");

        // ...the asset compiles from its editable .brr into the build .bin the assembler incbin's
        ninja.Should().Contain(
            "build build/assets/audio/AudioBRR_00.bin: audio_compile extracted/audio/AudioBRR_00.brr | generated/assets/audio/AudioBRR_00.json $binpack");
        // the commands rely on the manifest's ext (no --ext), matching the pinned binding shape
        ninja.Should().Contain("python $binpack compile --name $name $search_roots --out $out");
        // and the ROM depends on the compiled BRR .bin
        ninja.Should().Contain("build $out_rom: assemble | build/assets/audio/AudioBRR_00.bin");
    }

    [Fact]
    public void PerAssetEdgesDispatchByTypePrefixToTheMatchingBinding()
    {
        // Register a second codec binding and prove assets route to it by AssetType prefix,
        // independently of gfx -- the point of generalizing the hardcoded gfx-only wiring.
        var bindings = new[]
        {
            BuildFileGenerator.DefaultToolBindings[0], // gfx (reuses the shared $gfxpack var)
            new BuildToolBinding
            {
                TypePrefix = "audio.",
                ToolVar = "binpack",
                ToolFile = "binpack.py",
                SourceExtension = ".brr",
                CompiledExtension = ".bin",
                CompileRule = "audio_compile",
                ExtractRule = "audio_extract",
                CompileCommand = "python $binpack compile --name $name $search_roots --out $out",
                CompileDescription = "binpack compile $name",
                ExtractCommand = "python $binpack extract --manifest $manifest --rom $orig_rom --out $out $search_roots",
                ExtractDescription = "binpack extract $out",
            },
        };

        var ninja = new BuildFileGenerator(toolBindings: bindings).Generate([
            AssetNode("gfx/font", "gfx.snes.2bpp"), AssetNode("audio/song", "audio.snes.brr"),
        ]);

        // the second binding declared its OWN tool var (gfx reuses the shared one, so declares none)
        ninja.Should().Contain("binpack = tools/vendor/dizpack/binpack.py");
        ninja.Should().Contain("rule audio_compile");
        ninja.Should().Contain("rule audio_extract");

        // the audio asset extracts + compiles via the audio rules + $binpack...
        ninja.Should().Contain(
            "build extracted/audio/song.brr: audio_extract | generated/assets/audio/song.json $binpack $orig_rom");
        ninja.Should().Contain(
            "build build/assets/audio/song.bin: audio_compile extracted/audio/song.brr | generated/assets/audio/song.json $binpack");
        // ...while gfx still routes through gfx_compile + $gfxpack, unchanged.
        ninja.Should().Contain(
            "build build/assets/gfx/font.bin: gfx_compile extracted/gfx/font.png | generated/assets/gfx/font.json $gfxpack");
    }

    // ---- containers ------------------------------------------------------------------------

    [Fact]
    public void AProjectWithNoContainersGainsNothingThatServesOne()
    {
        // The whole container family -- the packing tool, the game-specific pipeline codecs, and
        // the buffer-mode rules -- is conditional. A project that packs nothing must produce the
        // build file it produced before packing existed, or every unrelated re-export shows up
        // as a diff and an inert rule block becomes impossible to tell from a live one.
        var ninja = new BuildFileGenerator().Generate([
            AssetNode("gfx/font"), AssetNode("audio/song", "audio.snes.brr"),
            AssetNode("text/names", "text.ct.mapped", "text/ct_8px.tbl"),
        ]);

        foreach (var token in new[]
                 {
                     "nodepack", "blob_slice", "blob_split", "blob_join",
                     "ctlz", "tools/vendor/game",
                     "gfx_decode", "gfx_encode", "audio_decode", "audio_encode",
                     "text_decode", "text_encode", "raw_decode", "raw_encode",
                 })
        {
            ninja.Should().NotContain(token, $"a container-free project must not mention {token}");
        }
    }

    [Fact]
    public void AContainerEmitsTwoEdgesPerMemberPlusFive()
    {
        // slice, decompress, split, join, compress -- plus decode + encode for each member.
        // The count is the point: split fans out in ONE edge and join fans in with ONE, so the
        // graph grows linearly and needs no scheduling machinery of its own.
        var ninja = new BuildFileGenerator().Generate([
            ContainerNode("blob/pack", compressed: true,
                AssetNode("gfx/a"), AssetNode("gfx/b"), AssetNode("gfx/c")),
        ]);

        BuildEdges(ninja).Count.Should().Be(2 * 3 + 5 + FixedEdges);
    }

    [Fact]
    public void ContainerEdgesChainSliceDecompressSplitDecodeEncodeJoinCompress()
    {
        var ninja = new BuildFileGenerator().Generate([
            ContainerNode("blob/pack", compressed: true,
                AssetNode("gfx/a"), AssetNode("text/b", "text.ct.mapped", "text/ct_8px.tbl")),
        ]);

        // the container's bytes come out of the ROM through its own manifest, exactly as a leaf
        // asset's do -- ROM ground truth, explicit manifest, never a layer lookup.
        ninja.Should().Contain(
            "build build/extract/blob/pack.raw: blob_slice | generated/assets/blob/pack.json $nodepack $orig_rom");

        // ...then the pipeline turns them into the buffer the members tile. The mode rides as an
        // edge variable because it is per-blob metadata the codec cannot derive.
        ninja.Should().Contain(
            "build build/extract/blob/pack.plain: ctlz_decompress build/extract/blob/pack.raw " +
            "| generated/assets/blob/pack.json $ctlz");
        ninja.Should().Contain("  lz_mode = 12");

        // ONE split edge, every member an output.
        ninja.Should().Contain(
            "build build/extract/gfx/a.bin build/extract/text/b.bin: blob_split " +
            "build/extract/blob/pack.plain | generated/assets/blob/pack.json $nodepack");
        ninja.Should().Contain("  outdir = build/extract");

        // each member decodes and encodes exactly like a top-level asset of its type would
        ninja.Should().Contain(
            "build extracted/gfx/a.png: gfx_decode build/extract/gfx/a.bin | generated/assets/gfx/a.json $gfxpack");
        ninja.Should().Contain(
            "build build/encode/gfx/a.bin: gfx_encode extracted/gfx/a.png | generated/assets/gfx/a.json $gfxpack");
        ninja.Should().Contain("  indir = extracted");

        // ONE join edge, every member an input: the buffer cannot be rebuilt from a subset.
        ninja.Should().Contain(
            "build build/join/blob/pack.plain: blob_join build/encode/gfx/a.bin " +
            "build/encode/text/b.bin | generated/assets/blob/pack.json $nodepack");
        ninja.Should().Contain("  indir = build/encode");

        // and the recompressed blob is what the assembler incbin's, so the ROM depends on it
        ninja.Should().Contain(
            "build build/assets/blob/pack.bin: ctlz_compress build/join/blob/pack.plain " +
            "| generated/assets/blob/pack.json $ctlzpack");
        ninja.Should().Contain("build $out_rom: assemble | build/assets/blob/pack.bin");
    }

    [Fact]
    public void PipelineCodecsComeFromTheGameToolDirAndTheSplitterFromTheSharedOne()
    {
        // Cutting a buffer at declared offsets is format-agnostic and ships with the codecs.
        // A compression format belongs to one game and must not: putting it in the shared dir is
        // what would ship it into every other game's repo.
        var ninja = new BuildFileGenerator().Generate([
            ContainerNode("blob/pack", compressed: true, AssetNode("gfx/a")),
        ]);

        ninja.Should().Contain("nodepack = tools/vendor/dizpack/nodepack.py");
        ninja.Should().Contain("ctlz = tools/vendor/game/ctlz.py");
        ninja.Should().Contain("ctlzpack = tools/vendor/game/ctlzpack.py");

        // the encoder knobs that are policy stay pinned inside the tool; only the per-blob
        // metadata it cannot derive appears on the command line.
        ninja.Should().Contain(
            "  command = python $ctlz decompress --in $in --out $out --expect-mode $lz_mode");
        ninja.Should().Contain(
            "  command = python $ctlzpack compress --in $in --out $out --mode $lz_mode");
        ninja.Should().NotContain("--tiebreak");
        ninja.Should().NotContain("--tailpad");
    }

    [Fact]
    public void AnUncompressedContainerHasNoPipelineEdges()
    {
        // Nothing between the stored bytes and the buffer means the two collapse: the slice IS
        // the buffer and the join output IS the incbin'd payload.
        var ninja = new BuildFileGenerator().Generate([
            ContainerNode("blob/plain", compressed: false, AssetNode("gfx/a"), AssetNode("gfx/b")),
        ]);

        BuildEdges(ninja).Count.Should().Be(2 * 2 + 3 + FixedEdges);
        ninja.Should().NotContain("ctlz");
        ninja.Should().NotContain(".plain");
        ninja.Should().Contain(
            "build build/extract/gfx/a.bin build/extract/gfx/b.bin: blob_split " +
            "build/extract/blob/plain.raw | generated/assets/blob/plain.json $nodepack");
        ninja.Should().Contain(
            "build build/assets/blob/plain.bin: blob_join build/encode/gfx/a.bin build/encode/gfx/b.bin");
    }

    [Fact]
    public void AMembersSharedFilesAreImplicitDepsOfItsDecodeEdge()
    {
        // Same rule as a top-level asset: editing the character table has to re-decode the text,
        // or the build keeps serving text rendered with the old glyph map.
        var ninja = new BuildFileGenerator().Generate([
            ContainerNode("blob/pack", compressed: true,
                AssetNode("text/credits", "text.ct.mapped", "text/ct_8px.tbl")),
        ]);

        ninja.Should().Contain(
            "build extracted/text/credits.yaml: text_decode build/extract/text/credits.bin " +
            "| generated/assets/text/credits.json $textpack assets/text/ct_8px.tbl");
    }

    [Fact]
    public void MembersAppearInTheExtractAliasButTheContainerDoesNot()
    {
        // A member has an editable source; the container has none -- what it holds is its
        // members, and they are already listed.
        var ninja = new BuildFileGenerator().Generate([
            ContainerNode("blob/pack", compressed: true, AssetNode("gfx/a"), AssetNode("gfx/b")),
        ]);

        ninja.Should().Contain("build extract: phony extracted/gfx/a.png extracted/gfx/b.png");
    }

    [Fact]
    public void ChainedPipelineStagesAreRejectedRatherThanGuessedAt()
    {
        var twoStages = new AssetBuildNode
        {
            Name = "blob/pack",
            AssetType = "blob.container",
            Pipeline =
            [
                new AssetPipelineStage { Codec = "compress.ct.lzss", BlockKey = "lz", Block = [] },
                new AssetPipelineStage { Codec = "compress.ct.lzss", BlockKey = "lz", Block = [] },
            ],
            Members = [AssetNode("gfx/a")],
        };

        var act = () => new BuildFileGenerator().Generate([twoStages]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*chained stages*");
    }

    [Fact]
    public void AnUnregisteredPipelineCodecFailsLoudlyNamingIt()
    {
        var unknown = new AssetBuildNode
        {
            Name = "blob/pack",
            AssetType = "blob.container",
            Pipeline = [new AssetPipelineStage { Codec = "compress.made.up", BlockKey = "lz", Block = [] }],
            Members = [AssetNode("gfx/a")],
        };

        var act = () => new BuildFileGenerator().Generate([unknown]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*compress.made.up*");
    }

    // ---- game-tool vendoring ---------------------------------------------------------------

    /// <summary>Build a Diz-shaped tools tree: shared codecs plus one game-specific set.</summary>
    private string MakeToolsTree(string rootName, params string[] gameKeys)
    {
        var tools = Path.Combine(tempDir, rootName, "tools", "dizpack");
        Directory.CreateDirectory(tools);
        File.WriteAllText(Path.Combine(tools, "gfxpack.py"), "# stub\n");
        File.WriteAllText(Path.Combine(tools, "requirements.txt"), "Pillow>=10\n");

        foreach (var key in gameKeys)
        {
            var gameDir = Path.Combine(tempDir, rootName, "tools", "dizpack-game", key);
            Directory.CreateDirectory(gameDir);
            File.WriteAllText(Path.Combine(gameDir, "ctlz.py"), "# stub decoder\n");
            File.WriteAllText(Path.Combine(gameDir, "ctlzpack.py"), "# stub encoder\n");
        }

        return tools;
    }

    [Fact]
    public void AContainerFreeProjectVendorsNoGameToolsAtAll()
    {
        // Not an empty directory either: a vendored tool nothing invokes is indistinguishable
        // from one that has quietly stopped being invoked.
        var tools = MakeToolsTree("clean-diz", "ct");
        var exportRoot = Path.Combine(tempDir, "clean-export");
        Directory.CreateDirectory(exportRoot);

        var generator = new BuildFileGenerator();
        var keys = generator.GameToolKeys([AssetNode("gfx/font")]);

        keys.Should().BeEmpty();
        new ToolVendoring().VendorGameToolsInto(exportRoot, keys, tools).Should().BeEmpty();
        Directory.Exists(Path.Combine(exportRoot, "tools", "vendor", "game")).Should().BeFalse();
    }

    [Fact]
    public void AContainerVendorsExactlyTheToolSetItsPipelineNames()
    {
        var tools = MakeToolsTree("game-diz", "ct", "unused");
        var exportRoot = Path.Combine(tempDir, "game-export");
        Directory.CreateDirectory(exportRoot);

        var generator = new BuildFileGenerator();
        var keys = generator.GameToolKeys([
            ContainerNode("blob/pack", compressed: true, AssetNode("gfx/a")),
        ]);

        keys.Should().Equal("ct");
        new ToolVendoring().VendorGameToolsInto(exportRoot, keys, tools);

        var vendored = Path.Combine(exportRoot, "tools", "vendor", "game");
        File.Exists(Path.Combine(vendored, "ctlz.py")).Should().BeTrue();
        File.Exists(Path.Combine(vendored, "ctlzpack.py")).Should().BeTrue();

        // the other game's tools are present in the Diz install and must stay there: keying the
        // source dir per game is the whole point of not putting these with the shared codecs.
        Directory.GetFiles(vendored).Should().HaveCount(2);
    }

    [Fact]
    public void AMissingGameToolSetFailsTheExportRatherThanShippingAnUnbuildableRepo()
    {
        // The generated build names these scripts by path, so a silent skip would move the
        // failure to whoever next runs ninja, with nothing pointing back at the export.
        var tools = MakeToolsTree("no-game-diz");
        var exportRoot = Path.Combine(tempDir, "no-game-export");
        Directory.CreateDirectory(exportRoot);

        var act = () => new ToolVendoring().VendorGameToolsInto(exportRoot, ["ct"], tools);

        act.Should().Throw<InvalidOperationException>().WithMessage("*'ct' tool set*");
    }

    [Fact]
    public void VendoringPicksUpNewlyAddedCodecsAutomatically()
    {
        // The vendor list is DISCOVERED, not a hardcoded pair: dropping a new codec (binpack.py)
        // into the source tools dir ships it without editing ToolVendoring. A stray subdir
        // (__pycache__) and a non-vendorable file must be skipped.
        var fakeTools = Path.Combine(tempDir, "src-tools");
        Directory.CreateDirectory(fakeTools);
        Directory.CreateDirectory(Path.Combine(fakeTools, "__pycache__"));
        File.WriteAllText(Path.Combine(fakeTools, "__pycache__", "gfxpack.cpython.pyc"), "junk");
        File.WriteAllText(Path.Combine(fakeTools, "gfxpack.py"), "# stub\n");
        File.WriteAllText(Path.Combine(fakeTools, "binpack.py"), "# stub2\n");
        File.WriteAllText(Path.Combine(fakeTools, "requirements.txt"), "Pillow>=10\n");

        var exportRoot = Path.Combine(tempDir, "vend-export");
        Directory.CreateDirectory(exportRoot);

        new ToolVendoring().VendorInto(exportRoot, fakeTools);

        var vendored = Path.Combine(exportRoot, "tools", "vendor", "dizpack");
        File.Exists(Path.Combine(vendored, "gfxpack.py")).Should().BeTrue();
        File.Exists(Path.Combine(vendored, "binpack.py")).Should()
            .BeTrue("a new codec must ship without editing ToolVendoring");
        File.Exists(Path.Combine(vendored, "requirements.txt")).Should().BeTrue();
        Directory.Exists(Path.Combine(vendored, "__pycache__")).Should()
            .BeFalse("subdirectories (bytecode caches) must not be vendored");
    }
}
