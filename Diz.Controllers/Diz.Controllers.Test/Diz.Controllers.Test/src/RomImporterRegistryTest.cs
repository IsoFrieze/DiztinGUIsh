#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Diz.Controllers.importers;
using Diz.Core.serialization;
using FluentAssertions;
using LightInject;
using Xunit;

namespace Diz.Controllers.Test;

/// <summary>
/// The platform-detection table: which importer a given file goes to, and what the file picker
/// offers. SNES is the only console registered in the app today, so most of what is pinned here is
/// that a single-importer registry stays as permissive about filenames as Diz has always been.
/// </summary>
public class RomImporterRegistryTest
{
    /// <summary>
    /// Stands in for a console importer. Nothing here imports anything -- selection and the filter
    /// string are decided entirely by <see cref="IRomImporter.PlatformName"/> and
    /// <see cref="IRomImporter.FileExtensions"/>.
    /// </summary>
    private sealed class FakeImporter(string platformName, params string[] extensions) : IRomImporter
    {
        public string PlatformName { get; } = platformName;
        public IReadOnlyList<string> FileExtensions { get; } = extensions;

        public Task<ImportRomSettings?> ChooseImportSettingsAsync(string romFilename) =>
            Task.FromResult<ImportRomSettings?>(null);
    }

    private static readonly IRomImporter SnesLike = new FakeImporter("SNES", ".smc", ".sfc");
    private static readonly IRomImporter OtherConsole = new FakeImporter("Other", ".other");

    private static RomImporterRegistry SoleImporter() => new([SnesLike]);
    private static RomImporterRegistry TwoImporters() => new([SnesLike, OtherConsole]);

    // ------------------------------------------------------------------ extension matching

    [Theory]
    [InlineData("game.smc")]
    [InlineData("game.sfc")]
    [InlineData(@"C:\roms\some folder\game.smc")]
    public void AClaimedExtensionPicksThatImporter(string filename)
    {
        TwoImporters().SelectFor(filename).Should().BeSameAs(SnesLike);
    }

    [Theory]
    [InlineData("game.SFC")]
    [InlineData("game.Sfc")]
    [InlineData("game.SMC")]
    [InlineData("GAME.OTHER")]
    public void MatchingIgnoresTheCaseOfTheExtension(string filename)
    {
        // filenames come from a file picker on a case-insensitive filesystem; ".SFC" and ".sfc"
        // are the same file to the OS and must be the same file here.
        var expected = filename.ToLowerInvariant().EndsWith(".other") ? OtherConsole : SnesLike;
        TwoImporters().SelectFor(filename).Should().BeSameAs(expected);
    }

    [Fact]
    public void EachImporterGetsItsOwnExtensions()
    {
        TwoImporters().SelectFor("game.other").Should().BeSameAs(OtherConsole);
    }

    // ------------------------------------------------------------------ the permissive fallback

    [Theory]
    [InlineData("game.bin")]
    [InlineData("game.rom")]
    [InlineData("game.nothing-like-a-rom")]
    public void AnUnclaimedExtensionStillGoesToTheOnlyImporter(string filename)
    {
        // ROMs get renamed. Diz has always imported a .bin or .rom without complaint, and with one
        // importer registered there is nothing to be ambiguous about -- refusing would be a
        // regression, not a safety check.
        SoleImporter().SelectFor(filename).Should().BeSameAs(SnesLike);
    }

    [Theory]
    [InlineData("game")]
    [InlineData("")]
    [InlineData(@"C:\roms\game")]
    public void NoExtensionAtAllStillGoesToTheOnlyImporter(string filename)
    {
        SoleImporter().SelectFor(filename).Should().BeSameAs(SnesLike);
    }

    [Theory]
    [InlineData("game.bin")]
    [InlineData("game")]
    public void WithMoreThanOneImporterAnUnclaimedNameSelectsNothing(string filename)
    {
        // the extension is the only evidence there is, and it points at neither console. Handing
        // the file to whichever importer happens to be first would produce a project analysed as
        // the wrong machine, so the answer is "can't tell" and the caller says so. This is the
        // case that calls for asking the user, once there is more than one console to ask about.
        TwoImporters().SelectFor(filename).Should().BeNull();
    }

    [Fact]
    public void AnEmptyRegistrySelectsNothing()
    {
        new RomImporterRegistry([]).SelectFor("game.smc").Should().BeNull();
    }

    // ------------------------------------------------------------------ the file picker's filter

    [Fact]
    public void TheFilterOffersEachImportersExtensionsAndAlwaysAllFiles()
    {
        TwoImporters().BuildFileDialogFilter()
            .Should().Be("SNES ROM Images|*.smc;*.sfc|Other ROM Images|*.other|All files|*.*");
    }

    [Fact]
    public void TheFilterKeepsAnAllFilesEntryEvenWithNothingRegistered()
    {
        // a ROM with an unusual or missing extension is importable, so there must always be a way
        // to see it in the picker.
        new RomImporterRegistry([]).BuildFileDialogFilter()
            .Should().Be(RomImporterRegistry.AllFilesFilterEntry);
    }

    // ------------------------------------------------------------------ what the app registers

    [Fact]
    public void TheRealSnesImporterClaimsTheSnesExtensions()
    {
        // null collaborators: nothing here runs an import, and what a console claims is fixed
        // metadata that must be readable without a ROM, a window or a container.
        var registry = new RomImporterRegistry([new SnesRomImporter(null!, null!, null!)]);

        registry.BuildFileDialogFilter()
            .Should().Be("SNES ROM Images|*.smc;*.sfc;*.swc;*.fig|All files|*.*");

        registry.SelectFor("game.sfc").Should().NotBeNull();
    }

    // ------------------------------------------------------------------ DI

    [Fact]
    public void TheContainerHandsTheRegistryEveryRegisteredImporter()
    {
        // the registry takes IEnumerable<IRomImporter>, which only works if the container collects
        // every registration of that service type. This pins the registration style the app uses:
        // one named registration per importer.
        var container = new ServiceContainer();
        container.Register<IRomImporter>(_ => SnesLike, "SnesRomImporter");
        container.Register<IRomImporter>(_ => OtherConsole, "OtherRomImporter");
        container.Register<RomImporterRegistry>();

        // no ordering assertion: LightInject does not hand these back in registration order, and
        // nothing but the cosmetic order of the file picker's entries depends on which comes first.
        var collected = container.GetInstance<RomImporterRegistry>().Importers;

        collected.Should().HaveCount(2);
        collected.Should().Contain(SnesLike).And.Contain(OtherConsole);
    }

    [Fact]
    public void ASingleRegistrationIsAlsoCollected()
    {
        // one registration is the app's actual situation, and a container that only handed over
        // collections of two or more would break it.
        var container = new ServiceContainer();
        container.Register<IRomImporter>(_ => SnesLike, "SnesRomImporter");
        container.Register<RomImporterRegistry>();

        container.GetInstance<RomImporterRegistry>().Importers.Single().Should().BeSameAs(SnesLike);
    }

    [Fact]
    public void TheRegistryCanBeInjectedAsAFactory()
    {
        // both consumers take Func<RomImporterRegistry> rather than the registry itself: the
        // importers it collects are transient (each drives one analysed ROM), so it is resolved at
        // the moment it is used, not once at construction.
        var container = new ServiceContainer();
        container.Register<IRomImporter>(_ => SnesLike, "SnesRomImporter");
        container.Register<RomImporterRegistry>();

        var create = container.GetInstance<Func<RomImporterRegistry>>();

        create().Importers.Single().Should().BeSameAs(SnesLike);
    }
}
