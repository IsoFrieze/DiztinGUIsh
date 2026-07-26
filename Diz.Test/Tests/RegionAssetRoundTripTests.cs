using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using Diz.LogWriter.assets;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests;

/// <summary>
/// End-to-end check that Diz's asset export and the external codec tool (gfxpack) agree.
///
/// Diz writes a manifest; gfxpack uses it to slice the ROM into a PNG and back. Nothing
/// guarantees the two stay in sync except a test that actually runs both, so this does:
///   Diz export -> .json  ->  gfxpack extract -> .png  ->  gfxpack compile -> .bin
/// and asserts the final bytes equal the original ROM bytes.
///
/// Gated on env vars rather than hardcoded paths/offsets, so it's skipped (not broken) on
/// machines without a ROM or python, and works with any game. Set ALL of these to run it:
///   DIZ_TEST_ROM         full path to a headerless SNES ROM
///   DIZ_TEST_GFXPACK     full path to gfxpack.py
///   DIZ_TEST_GFX_OFFSET  ROM (PC) offset of an uncompressed gfx block, 0x-hex or decimal
///                        (e.g. 0x40000)
///   DIZ_TEST_GFX_LENGTH  length in bytes; must be a whole number of tiles (bpp*8 bytes each)
///   DIZ_TEST_GFX_BPP     bit depth of the block: 2, 4, or 8
/// </summary>
public class RegionAssetRoundTripTests
{
    // Linear HiROM-style mapping used only to round-trip PC offsets through SNES addresses;
    // the codepath under test doesn't care which mapping the real game uses.
    private const int HiRomBase = 0xC00000;

    private static string RomPath => Environment.GetEnvironmentVariable("DIZ_TEST_ROM");
    private static string GfxPackPath => Environment.GetEnvironmentVariable("DIZ_TEST_GFXPACK");
    private static int? GfxOffset => ParseIntEnv("DIZ_TEST_GFX_OFFSET");
    private static int? GfxLength => ParseIntEnv("DIZ_TEST_GFX_LENGTH");
    private static int? GfxBpp => ParseIntEnv("DIZ_TEST_GFX_BPP");

    private static bool CanRun =>
        File.Exists(RomPath ?? "") && File.Exists(GfxPackPath ?? "") &&
        GfxOffset != null && GfxLength != null && GfxBpp != null;

    /// <summary>Parse an env var as an int, accepting "0x" hex or plain decimal. Null if unset/invalid.</summary>
    private static int? ParseIntEnv(string name)
    {
        var raw = Environment.GetEnvironmentVariable(name)?.Trim();
        if (string.IsNullOrEmpty(raw))
            return null;

        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(raw[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)
                ? hex : null;

        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var dec)
            ? dec : null;
    }

    private class RomFileByteSource : IReadOnlyByteSource
    {
        private readonly byte[] rom;
        public RomFileByteSource(byte[] rom) => this.rom = rom;
        public byte? GetRomByte(int offset) =>
            offset >= 0 && offset < rom.Length ? rom[offset] : null;
        public int? GetRomWord(int offset) => null;
        public int? GetRomLong(int offset) => null;
        public int? GetRomDoubleWord(int offset) => null;
    }

    private class HiRomConverter : ISnesAddressConverter
    {
        public int ConvertSnesToPc(int offset) => offset >= HiRomBase ? offset - HiRomBase : -1;
        public int ConvertPCtoSnes(int offset) => offset + HiRomBase;
    }

    private static Region MakeGfxRegion(int pcOffset, int length, int bpp) => new()
    {
        RegionName = "font",
        AssetName = "gfx/font",
        StartSnesAddress = HiRomBase + pcOffset,
        EndSnesAddress = HiRomBase + pcOffset + length - 1,
        ExportType = RegionExportType.Asset,
        AssetType = $"gfx.snes.{bpp}bpp",
    };

    private static RegionAssetExportService MakeService(byte[] rom) => new(
        new RomFileByteSource(rom),
        new HiRomConverter(),
        [new BinaryRegionAssetExporter(), new GfxRegionAssetExporter()]);

    [Fact]
    public void DizExportedAssetRoundTripsThroughGfxPackByteIdentically()
    {
        // no-op when the env vars aren't set (xunit has no built-in skip, and this isn't
        // worth a new package dependency). CI/devs who set them get the real check.
        if (!CanRun)
            return;

        var (offset, length, bpp) = (GfxOffset.Value, GfxLength.Value, GfxBpp.Value);

        var rom = File.ReadAllBytes(RomPath);
        rom.Length.Should().BeGreaterThanOrEqualTo(offset + length,
            "the gfx block must lie inside the ROM (a 512-byte copier header would also shift every offset)");

        var expected = rom[offset..(offset + length)];

        var workDir = Path.Combine(Path.GetTempPath(), "diz-roundtrip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            // 1. Diz exports the region as a manifest -- and nothing else.
            var manifestRoot = Path.Combine(workDir, "generated", "assets");
            var directive = MakeService(rom).ExportRegion(MakeGfxRegion(offset, length, bpp), manifestRoot);
            directive.Should().Be("incbin \"build/assets/gfx/font.bin\"");

            var manifest = Path.Combine(manifestRoot, "gfx", "font.json");
            File.Exists(manifest).Should().BeTrue();

            // 2. gfxpack slices the ROM per DIZ'S manifest and renders a PNG. The manifest is
            //    named explicitly, so this exercises Diz's manifest -- not one the codec wrote
            //    for itself, which would trivially agree with itself and test nothing.
            var png = Path.Combine(workDir, "extracted", "gfx", "font.png");
            RunGfxPack(workDir,
                $"extract --manifest \"{manifest}\" --rom \"{RomPath}\" --out \"{png}\"");

            File.Exists(png).Should().BeTrue();

            // 3. gfxpack compiles the PNG back to raw bytes
            var rebuilt = Path.Combine(workDir, "rebuilt.bin");
            RunGfxPack(workDir,
                $"compile --name gfx/font --base-manifests \"{manifestRoot}\" " +
                $"--base-content \"{Path.Combine(workDir, "extracted")}\" --out \"{rebuilt}\"");

            // 4. the whole loop must be byte-identical
            File.ReadAllBytes(rebuilt).Should().Equal(expected,
                "Diz -> manifest -> PNG -> .bin must reproduce the original ROM bytes exactly");
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// The companion to the round-trip test: proves the check can actually fail.
    /// The manifest's source_sha256 is what catches someone extracting against the wrong ROM,
    /// so gfxpack must refuse when the ROM's bytes don't hash to what Diz recorded, rather
    /// than decoding a wrong-but-plausible PNG. Without this, a passing round-trip proves
    /// nothing.
    /// </summary>
    [Fact]
    public void GfxPackRefusesWhenTheRomDoesNotMatchTheManifestHash()
    {
        if (!CanRun)
            return;

        var (offset, length, bpp) = (GfxOffset.Value, GfxLength.Value, GfxBpp.Value);

        var rom = File.ReadAllBytes(RomPath);
        var workDir = Path.Combine(Path.GetTempPath(), "diz-negative-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            var manifestRoot = Path.Combine(workDir, "generated", "assets");
            MakeService(rom).ExportRegion(MakeGfxRegion(offset, length, bpp), manifestRoot);
            var manifest = Path.Combine(manifestRoot, "gfx", "font.json");

            // flip a byte in a COPY of the ROM, so the slice no longer hashes to what the
            // manifest recorded -- exactly the "built against the wrong ROM" case.
            var wrongRom = Path.Combine(workDir, "wrong.sfc");
            rom[offset + length / 2] ^= 0xFF;
            File.WriteAllBytes(wrongRom, rom);

            var png = Path.Combine(workDir, "extracted", "gfx", "font.png");
            var exitCode = RunGfxPackRaw(workDir,
                $"extract --manifest \"{manifest}\" --rom \"{wrongRom}\" --out \"{png}\"");

            exitCode.Should().NotBe(0,
                "gfxpack must reject a ROM whose bytes disagree with the manifest's sha256");
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>Run gfxpack and assert it succeeded.</summary>
    private static void RunGfxPack(string workDir, string args)
    {
        var (exitCode, stdout, stderr) = Exec(workDir, args);
        exitCode.Should().Be(0, $"gfxpack {args} failed.\nstdout:\n{stdout}\nstderr:\n{stderr}");
    }

    /// <summary>Run gfxpack and return the exit code, for cases where failure is expected.</summary>
    private static int RunGfxPackRaw(string workDir, string args) => Exec(workDir, args).exitCode;

    private static (int exitCode, string stdout, string stderr) Exec(string workDir, string args)
    {
        var psi = new ProcessStartInfo("python", $"\"{GfxPackPath}\" {args}")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi);
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return (proc.ExitCode, stdout, stderr);
    }
}
