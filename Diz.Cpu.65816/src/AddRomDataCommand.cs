using System.Collections.Generic;
using System.Linq;
using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using FluentValidation;
using JetBrains.Annotations;

namespace Diz.Cpu._65816;

/// <summary>
/// After a project file is deserialized from XML, it contains all its info EXCEPT
/// we don't store the bytes of the actual ROM file in the project file (for copyright and redundancy reasons).
/// So, on post-serialization we need to add the bytes from the ROM file on disk into our newly deserialized Project class
/// We also need to verify that we have the right file by running checks (like rom title and checksum) that we
/// indeed have the right file on disk/etc.
///
/// This class is designed to open a ROM file on disk and safely copy it into a Project file, ensuring data integrity.
/// </summary>
[UsedImplicitly]
public class AddRomDataCommand : IAddRomDataCommand
{
    // globalRomRegistry is OPTIONAL. It's wired as an optional constructor dependency (see
    // CoreServices), so it arrives null when the global ROM registry isn't registered. Null simply
    // switches the registry fallback + auto-populate off; the rest of the ROM search is unchanged.
    public AddRomDataCommand(Func<ILinkedRomBytesProvider> createLinkedProvider, IGlobalRomRegistry? globalRomRegistry = null)
    {
        this.createLinkedProvider = createLinkedProvider;
        this.globalRomRegistry = globalRomRegistry;
    }

    public bool ShouldProjectCartTitleMatchRomBytes { get; set; } = true;
    public ProjectXmlSerializer.Root? Root { get; set; } = null;
    public Func<string, string>? GetNextRomFileToTry { get; set; }
    public IMigrationRunner? MigrationRunner { get; set; }

    private Project? Project => Root?.Project ?? null;

    public void TryReadAttachedProjectRom()
    {
        if (Root?.Project == null)
            throw new InvalidDataException("Root element should contain a Project element, but none was found.");

        MigrationRunner?.OnLoadingBeforeAddLinkedRom(this);
        Populate();
        MigrationRunner?.OnLoadingAfterAddLinkedRom(this);
    }

    private void Populate()
    {
        // for copyright reasons, normally, we don't store the actual bytes from the ROM in the XML directly.
        // we only save metadata about them, and we populate them from the ROM file on disk as the last step
        // after the project is finished loading.
        //
        // However, different project loaders or generators may choose to do this (such as the sample data generator,
        // or, for test roms). So, don't try and load anything from a ROM file in disk if something else already
        // populated the bytes in the project.
        if (Project?.Data?.RomBytesLoaded ?? false)
            return;

        // Normal case: find a ROM file on disk matching our 
        FillIfSearchFoundRom();
    }

    private void FillIfSearchFoundRom()
    {
        if (Project == null)
            throw new InvalidOperationException("Project not allowed to be null here");
        
        var romFileData = SearchForValidRom();
        if (romFileData == null)
            throw new InvalidOperationException("Search failed, couldn't find compatible ROM to link");

        var (filename, romBytes) = romFileData.Value;
        
        Project.AttachedRomFilename = filename;
        Project.Data.RomBytes.CopyRomDataIn(romBytes);
    }

    private readonly Func<ILinkedRomBytesProvider> createLinkedProvider;
    private readonly IGlobalRomRegistry? globalRomRegistry;

    private (string filename, byte[] romBytes)? SearchForValidRom()
    {
        var searchProvider = createLinkedProvider();
        searchProvider.EnsureCompatible = (romFilename, romBytes) => EnsureProjectCompatibleWithRom(romBytes);

        // Fallback chain for locating the ROM. The search always starts from the project's own
        // AttachedRomFilename (its user-prefs). When that misses - typically a fresh checkout or a
        // sibling worktree that has no user-prefs file of its own - try any paths recorded in the
        // machine-global ROM registry BEFORE bothering the user with a file prompt. Only if those are
        // exhausted (or none match) do we fall through to asking the user.
        //
        // Crucially, registry paths get no special trust: they re-enter the same loop as any other
        // candidate and are validated by EnsureCompatible just like a user-picked file. That check is
        // header-only, though - it compares the 4 ROM-header complement/checksum bytes and (only when
        // enabled) the cartridge title; it does NOT hash ROM contents, and the cart-title comparison is
        // itself switched off for some older projects during migration. So a wrong ROM that happens to
        // share those header fields would pass; a stale/wrong entry that DOESN'T is simply skipped.
        var registryCandidates = new Queue<string>(GetGlobalRegistryRomCandidates());
        searchProvider.GetNextFilename = reasonWhyLastFileNotCompatible =>
        {
            while (registryCandidates.Count > 0)
            {
                var candidate = registryCandidates.Dequeue();
                if (!string.IsNullOrWhiteSpace(candidate))
                    return candidate;
            }
            return GetNextRomFileToTry?.Invoke(reasonWhyLastFileNotCompatible) ?? null;
        };

        // some other hints for the user for what ROM they should be looking for
        var extraPromptText = $"{Project?.InternalRomGameName ?? ""}";

        var result = searchProvider.SearchAndReadFromCompatibleRom(
            initialRomFile: Project?.AttachedRomFilename ?? "",
            extraPromptText
            );

        // Record the ROM we ended up using so a future open from a different checkout/worktree can
        // find it straight away instead of prompting. Cheap idempotent upsert; still only a hint.
        if (result != null)
            RememberRomInGlobalRegistry(result.Value.filename);

        return result;
    }

    private IEnumerable<string> GetGlobalRegistryRomCandidates()
    {
        if (globalRomRegistry == null || Project == null)
            return Enumerable.Empty<string>();

        return globalRomRegistry.FindCandidateRomPaths(Project.InternalCheckSum, Project.InternalRomGameName);
    }

    private void RememberRomInGlobalRegistry(string romFilename)
    {
        if (globalRomRegistry == null || Project == null || string.IsNullOrWhiteSpace(romFilename))
            return;

        // Skip the load+save of the machine-global file when it already records this exact path for
        // this ROM identity - the common case where the path came straight from the project's own
        // prefs and nothing changed. Avoids needlessly widening the concurrent-write window on every
        // open. Compare on the normalized path since that's the form the registry stores and returns.
        string comparablePath;
        try { comparablePath = Path.GetFullPath(romFilename); }
        catch { comparablePath = romFilename; }

        var alreadyStored = globalRomRegistry
            .FindCandidateRomPaths(Project.InternalCheckSum, Project.InternalRomGameName)
            .Any(p => string.Equals(p, comparablePath, StringComparison.OrdinalIgnoreCase));
        if (alreadyStored)
            return;

        globalRomRegistry.Remember(Project.InternalCheckSum, Project.InternalRomGameName, romFilename);
    }

    private void EnsureProjectCompatibleWithRom(byte[] romFileBytes)
    {
        var container = new RomToProjectAssociation
        {
            Project = Project,
            RomBytes = romFileBytes,
        };
        var validator = CreateValidator();
        var results = validator.Validate(container);
        if (!results.IsValid)
            throw new InvalidDataException(results.ToString());
    }

    private IValidator<RomToProjectAssociation> CreateValidator() =>
        CreateValidator(ShouldProjectCartTitleMatchRomBytes);

    private static IValidator<RomToProjectAssociation> CreateValidator(bool shouldProjectCartTitleMatchRomBytes)
    {
        var validator = new AddRomDataCommandValidator
        {
            EnsureProjectAndRomCartTitleMatch = shouldProjectCartTitleMatchRomBytes
        };
        return validator;
    }
}