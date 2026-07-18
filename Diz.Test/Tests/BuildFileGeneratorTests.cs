using System;
using System.IO;
using System.Linq;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
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

    private static Region AssetRegion(string name, string assetType = "gfx.snes.2bpp") => new()
    {
        RegionName = name,
        AssetName = name,
        StartSnesAddress = 0xC00000,
        EndSnesAddress = 0xC00040,
        ExportType = RegionExportType.Asset,
        AssetType = assetType,
    };

    [Fact]
    public void OnlyAssetRegionsAreCollected()
    {
        var regions = new IRegion[]
        {
            AssetRegion("gfx/font"),
            new Region { RegionName = "plain", ExportType = RegionExportType.Assembly },
            new Region { RegionName = "raw", AssetName = "data/raw", ExportType = RegionExportType.Binary },
        };

        var assets = BuildFileGenerator.CollectAssets(regions);

        // Binary regions are incbin'd directly and need no codec step, so the build has
        // nothing to rebuild for them -- only Asset regions belong here.
        assets.Should().ContainSingle().Which.Name.Should().Be("gfx/font");
    }

    [Fact]
    public void AssetsAreSortedSoOutputIsDeterministic()
    {
        // build.ninja is checked in, so unstable ordering would produce spurious diffs
        // on every export.
        var assets = BuildFileGenerator.CollectAssets([
            AssetRegion("gfx/zebra"), AssetRegion("gfx/apple"), AssetRegion("gfx/mango"),
        ]);

        assets.Select(a => a.Name).Should().ContainInOrder("gfx/apple", "gfx/mango", "gfx/zebra");
    }

    [Fact]
    public void GeneratedBuildWiresAssetsIntoTheRom()
    {
        var ninja = new BuildFileGenerator().Generate(BuildFileGenerator.CollectAssets([
            AssetRegion("gfx/font"),
        ]));

        // the PNG is the input the codec compiles from...
        ninja.Should().Contain("build build/assets/gfx/font.bin: gfx_compile assets/src/gfx/font.png");
        // ...and the manifest is a dependency, so editing it also triggers a rebuild
        ninja.Should().Contain("assets/src/gfx/font.json");

        // The ROM must depend on the compiled asset. Without this edge, editing a PNG would
        // recompile the .bin but never re-assemble, and the ROM would silently go stale.
        ninja.Should().Contain("build $out_rom: assemble | build/assets/gfx/font.bin");

        ninja.Should().Contain("build verify: sha_verify $out_rom");
        ninja.Should().Contain("default $out_rom");
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

        // mod layers come first; assets/src (the complete base layer) is always last
        ninja.Should().Contain("search_roots = $mod_roots --search assets/src");
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
        new BuildFileGenerator().WriteTo(tempDir, BuildFileGenerator.CollectAssets([AssetRegion("gfx/font")]));

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
        ninja.Should().NotContain("gfx_compile assets/");
        ninja.Should().Contain("build seed: phony");
    }

    [Fact]
    public void GeneratedBuildIsStableAcrossRuns()
    {
        var assets = BuildFileGenerator.CollectAssets([AssetRegion("gfx/a"), AssetRegion("gfx/b")]);
        var first = new BuildFileGenerator().Generate(assets);
        var second = new BuildFileGenerator().Generate(assets);

        second.Should().Be(first, "regenerating must produce a clean diff, not churn");
    }

    [Fact]
    public void WriteToProducesBuildNinjaAtExportRoot()
    {
        new BuildFileGenerator().WriteTo(tempDir, BuildFileGenerator.CollectAssets([AssetRegion("gfx/f")]));

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
        // setup must point at the vendored requirements, or Pillow never gets installed
        doc.Should().Contain("pip install -r tools/vendor/dizpack/requirements.txt");
        doc.Should().Contain("pip install ninja");
        doc.Should().Contain("./build-assets.sh");
        doc.Should().Contain("ninja verify");
        doc.Should().Contain("ninja seed");
        doc.Should().Contain("assets/src");
    }

    [Fact]
    public void VendoringFindsToolsByWalkingUpFromABinDirectory()
    {
        // Running from bin/Debug/net9.0-windows/ must still locate tools/dizpack.
        var root = Path.Combine(tempDir, "walkup");
        var tools = Path.Combine(root, "tools", "dizpack");
        Directory.CreateDirectory(tools);
        File.WriteAllText(Path.Combine(tools, "gfxpack.py"), "# stub\n");
        File.WriteAllText(Path.Combine(tools, "requirements.txt"), "Pillow>=10\n");

        var deep = Path.Combine(root, "bin", "Debug", "net9.0-windows");
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
}
