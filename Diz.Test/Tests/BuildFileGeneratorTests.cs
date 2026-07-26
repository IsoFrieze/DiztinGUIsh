using System;
using System.IO;
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
