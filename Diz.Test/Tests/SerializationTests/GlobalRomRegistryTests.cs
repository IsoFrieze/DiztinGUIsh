using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Diz.Core.model.project;
using Diz.Core.serialization;
using Diz.Test.Utils;
using FluentAssertions;
using LightInject;
using Xunit;

namespace Diz.Test.Tests.SerializationTests;

// Covers the machine-global ROM registry: the fallback Diz uses to locate a project's ROM when the
// project's own (gitignored) user-prefs don't point at one. See GlobalRomRegistry.
public class GlobalRomRegistryTests : IDisposable
{
    private readonly string tempDir;
    private readonly string registryPath;

    private const uint CtChecksum = 2022475635;
    private const string CtGameName = "CHRONO TRIGGER       ";

    public GlobalRomRegistryTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "diz-rom-registry-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        registryPath = Path.Combine(tempDir, "global-rom-registry.xml");
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void RemembersThenFindsARomPath()
    {
        var reg = new GlobalRomRegistry(registryPath);
        const string romPath = @"C:\some\where\ct-orig.smc";

        reg.Remember(CtChecksum, CtGameName, romPath);

        File.Exists(registryPath).Should().BeTrue();
        reg.FindCandidateRomPaths(CtChecksum, CtGameName).Should().ContainSingle().Which.Should().Be(romPath);
    }

    [Fact]
    public void RememberIsAnUpsert_UpdatesPathForSameIdentity()
    {
        var reg = new GlobalRomRegistry(registryPath);
        reg.Remember(CtChecksum, CtGameName, @"C:\old\ct.smc");
        reg.Remember(CtChecksum, CtGameName, @"C:\new\ct.smc");

        reg.FindCandidateRomPaths(CtChecksum, CtGameName)
            .Should().ContainSingle().Which.Should().Be(@"C:\new\ct.smc");
    }

    [Fact]
    public void FindReturnsEmptyWhenNothingMatches()
    {
        var reg = new GlobalRomRegistry(registryPath);
        reg.Remember(CtChecksum, CtGameName, @"C:\x\ct.smc");

        reg.FindCandidateRomPaths(999u, "SOME OTHER GAME").Should().BeEmpty();
    }

    [Fact]
    public void FindReturnsEmptyWhenFileMissing()
    {
        new GlobalRomRegistry(registryPath).FindCandidateRomPaths(CtChecksum, CtGameName).Should().BeEmpty();
    }

    [Fact]
    public void BailsOnUnexpectedSchemaVersion()
    {
        // a file from a hypothetical future/incompatible schema: we must ignore it, not migrate it.
        File.WriteAllText(registryPath,
            $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            $"<DizGlobalRomRegistry Version=\"{GlobalRomRegistry.ExpectedVersion + 1}\">" +
            $"<Rom InternalCheckSum=\"{CtChecksum}\" InternalRomGameName=\"{CtGameName}\" Path=\"C:\\x\\ct.smc\" />" +
            $"</DizGlobalRomRegistry>");

        var reg = new GlobalRomRegistry(registryPath);
        reg.FindCandidateRomPaths(CtChecksum, CtGameName).Should().BeEmpty();

        // and a Remember() against that incompatible file must leave it untouched (no migration/clobber)
        var before = File.ReadAllText(registryPath);
        reg.Remember(CtChecksum, CtGameName, @"C:\y\ct.smc");
        File.ReadAllText(registryPath).Should().Be(before);
    }

    [Fact]
    public void CorruptFileIsIgnoredGracefully()
    {
        File.WriteAllText(registryPath, "this is not xml at all {{{");
        new GlobalRomRegistry(registryPath).FindCandidateRomPaths(CtChecksum, CtGameName).Should().BeEmpty();
    }

    [Fact]
    public void NonMatchingGameName_IsFilteredOut_EvenWhenChecksumMatches()
    {
        // Cheap early-out: an entry that shares the checksum but not the game name is excluded, so the
        // ROM search never opens it. Only the fully-matching entry (checksum AND name) is handed back.
        var reg = new GlobalRomRegistry(registryPath);
        reg.Remember(CtChecksum, "SOMETHING ELSE", @"C:\a\other.smc");
        reg.Remember(CtChecksum, CtGameName, @"C:\b\ct.smc");

        reg.FindCandidateRomPaths(CtChecksum, CtGameName)
            .Should().ContainSingle().Which.Should().Be(@"C:\b\ct.smc");
    }

    [Fact]
    public void GameNameMatchesIgnoringHeaderPadding()
    {
        // header titles are space-padded; a lookup with the trimmed name still matches a padded entry.
        var reg = new GlobalRomRegistry(registryPath);
        reg.Remember(CtChecksum, CtGameName, @"C:\b\ct.smc"); // stored padded
        reg.FindCandidateRomPaths(CtChecksum, "CHRONO TRIGGER")
            .Should().ContainSingle().Which.Should().Be(@"C:\b\ct.smc");
    }

    [Fact]
    public void MalformedFileIsRecovered_RewrittenAsValidXmlContainingTheNewEntry()
    {
        // a file left half-written by a crash/force-kill (well-formed prefix, truncated body): a later
        // Remember must NOT be permanently blocked by it - it recovers the file and writes the entry.
        File.WriteAllText(registryPath,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<DizGlobalRomRegistry Version=\"1\"><Rom Internal");

        var reg = new GlobalRomRegistry(registryPath);
        reg.Remember(CtChecksum, CtGameName, @"C:\recovered\ct.smc");

        // parses again (would throw if still malformed) and holds the new entry
        var doc = XDocument.Load(registryPath);
        doc.Root!.Name.LocalName.Should().Be("DizGlobalRomRegistry");
        reg.FindCandidateRomPaths(CtChecksum, CtGameName)
            .Should().ContainSingle().Which.Should().Be(@"C:\recovered\ct.smc");
    }

    [Fact]
    public void SuccessfulSave_LeavesNoLingeringTempFile()
    {
        // the atomic write goes through a sibling ".tmp" then File.Move; nothing should be left behind.
        var reg = new GlobalRomRegistry(registryPath);
        reg.Remember(CtChecksum, CtGameName, @"C:\x\ct.smc");

        File.Exists(registryPath).Should().BeTrue();
        File.Exists(registryPath + ".tmp").Should().BeFalse();
    }

    [Fact]
    public void RememberLeavesWellFormedUnknownVersionFileByteIdentical()
    {
        // self-heal must not clobber a well-formed file we simply don't understand (deliberate: no
        // migration). Only MALFORMED xml recovers; an unknown Version is left exactly as-is.
        var contents =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            $"<DizGlobalRomRegistry Version=\"{GlobalRomRegistry.ExpectedVersion + 7}\" />";
        File.WriteAllText(registryPath, contents);

        var reg = new GlobalRomRegistry(registryPath);
        reg.Remember(CtChecksum, CtGameName, @"C:\y\ct.smc");

        File.ReadAllText(registryPath).Should().Be(contents);
    }

    [Fact]
    public void Remember_NormalizesStoredPath_SoEquivalentSpellingsStayOneEntry()
    {
        var reg = new GlobalRomRegistry(registryPath);
        reg.Remember(CtChecksum, CtGameName, @"C:\a\ct.smc");
        reg.Remember(CtChecksum, CtGameName, @"C:\a\..\a\ct.smc"); // same file, non-normalized spelling

        reg.FindCandidateRomPaths(CtChecksum, CtGameName)
            .Should().ContainSingle().Which.Should().Be(@"C:\a\ct.smc");
    }

    [Fact]
    public void Find_NormalizesStoredPaths()
    {
        // a registry written with a non-normalized path (e.g. by an older build or by hand) is
        // normalized on the way out, so lookups don't depend on the exact spelling that was stored.
        File.WriteAllText(registryPath,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            $"<DizGlobalRomRegistry Version=\"{GlobalRomRegistry.ExpectedVersion}\">" +
            $"<Rom InternalCheckSum=\"{CtChecksum}\" InternalRomGameName=\"{CtGameName}\" Path=\"C:\\a\\..\\a\\ct.smc\" />" +
            "</DizGlobalRomRegistry>");

        new GlobalRomRegistry(registryPath).FindCandidateRomPaths(CtChecksum, CtGameName)
            .Should().ContainSingle().Which.Should().Be(@"C:\a\ct.smc");
    }
}

// Proves the DI graph still resolves after AddRomDataCommand gained its IGlobalRomRegistry dependency.
public class GlobalRomRegistryServiceResolutionTests : ContainerFixture
{
    [Fact]
    public void RegistryAndRomCommandResolve()
    {
        ServiceFactory.GetInstance<IGlobalRomRegistry>().Should().NotBeNull();
        ServiceFactory.GetInstance<IAddRomDataCommand>().Should().NotBeNull();
    }
}
