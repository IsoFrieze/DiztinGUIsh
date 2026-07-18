using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Diz.LogWriter.assets;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests;

/// <summary>
/// Runs the Diz-generated build against a real game repo: ninja + asar + gfxpack, ending in
/// a whole-ROM SHA comparison.
///
/// This is the only test that proves the generated build.ninja is actually valid ninja and
/// that the pieces compose. Unit tests can only check the text we emit; they cannot tell us
/// ninja will accept it.
///
/// Gated on env vars so it skips cleanly where the repo/tools aren't present:
///   DIZ_TEST_GAMEREPO  path to the game repo (must contain generated/main.asm and a
///                      build-config.ninja -- the user-owned, game-specific half, which
///                      this test never creates or modifies)
///   DIZ_TEST_GFXPACK   path to gfxpack.py
/// Also skips unless the ROM named by `orig_rom` in build-config.ninja exists.
/// </summary>
public class NinjaBuildIntegrationTests
{
    private static string GameRepo => Environment.GetEnvironmentVariable("DIZ_TEST_GAMEREPO");
    private static string GfxPack => Environment.GetEnvironmentVariable("DIZ_TEST_GFXPACK");

    private static bool CanRun =>
        !string.IsNullOrEmpty(GameRepo) && Directory.Exists(GameRepo) && File.Exists(GfxPack ?? "");

    [Fact]
    public void GeneratedNinjaBuildRebuildsTheRomByteIdentically()
    {
        if (!CanRun)
            return;

        // build-config.ninja is the user-owned, game-specific half. This test requires it
        // to already exist and must NEVER create or modify it.
        var configPath = Path.Combine(GameRepo, BuildFileGenerator.ConfigFileName);
        if (!File.Exists(configPath))
            return;

        var origRom = ParseOrigRom(configPath);
        if (origRom == null || !File.Exists(Path.Combine(GameRepo, origRom)))
            return; // no reference ROM to compare against; nothing meaningful to assert

        // We write build.ninja into a REAL repo, so back up whatever is there and restore it
        // afterward. A test that silently leaves its own config behind would look harmless
        // and then show up as a mystery diff in someone's working tree.
        var ninjaPath = Path.Combine(GameRepo, "build.ninja");
        var backup = File.Exists(ninjaPath) ? File.ReadAllText(ninjaPath) : null;

        try
        {
            RunBuildAndVerify();
        }
        finally
        {
            if (backup != null)
                File.WriteAllText(ninjaPath, backup);
        }
    }

    /// <summary>Pull the `orig_rom = ...` value out of build-config.ninja, or null.</summary>
    private static string ParseOrigRom(string configPath) =>
        File.ReadAllLines(configPath)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0].Trim() == "orig_rom")
            .Select(parts => parts[1].Trim())
            .FirstOrDefault(value => value.Length > 0);

    private static void RunBuildAndVerify()
    {
        // defaults for everything Diz owns; the game-specific half comes from the repo's
        // own build-config.ninja, which WriteTo leaves untouched because it already exists.
        var settings = new BuildFileGeneratorSettings { MainAsmPath = "generated/main.asm" };

        new BuildFileGenerator(settings).WriteTo(GameRepo, []);

        var vendoring = new ToolVendoring();
        vendoring.VendorInto(GameRepo, Path.GetDirectoryName(GfxPack));
        vendoring.WriteWrapper(GameRepo);

        File.Exists(Path.Combine(GameRepo, "build.ninja")).Should().BeTrue();
        File.Exists(Path.Combine(GameRepo, "tools", "vendor", "dizpack", "gfxpack.py"))
            .Should().BeTrue("the repo must be able to build without Diz present");

        // Build, then verify. 'verify' is the target that actually asserts byte-identity.
        var (buildCode, buildOut, buildErr) = Run(GameRepo, "ninja", "");
        buildCode.Should().Be(0, $"ninja build failed.\n{buildOut}\n{buildErr}");

        var (verifyCode, verifyOut, verifyErr) = Run(GameRepo, "ninja", "verify");
        verifyCode.Should().Be(0,
            $"the rebuilt ROM does not match the original.\n{verifyOut}\n{verifyErr}");
        verifyOut.Should().Contain("BYTE-IDENTICAL");
    }

    private static (int code, string stdout, string stderr) Run(string workDir, string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
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
