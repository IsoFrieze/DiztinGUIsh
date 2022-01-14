using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using FluentValidation;

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
public class AddRomDataCommand : IAddRomDataCommand
{
    public bool ShouldProjectCartTitleMatchRomBytes { get; set; } = true;
    public ProjectSerializedRoot? Root { get; set; } = null;
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
        var result = SearchForValidRom();
        if (result == null)
            throw new InvalidOperationException("Search failed, couldn't find compatible ROM to link");

        Project.AttachedRomFilename = result.Value.filename;
        Project.Data.RomBytes.CopyRomDataIn(result.Value.romBytes);
    }

    private static ILinkedRomBytesProvider GetLinkedRomProvider() =>
        new LinkedRomBytesFileSearchProvider();

    private (string filename, byte[] romBytes)? SearchForValidRom()
    {
        var searchProvider = GetLinkedRomProvider();
        searchProvider.EnsureCompatible = (romFilename, romBytes) => EnsureProjectCompatibleWithRom(romBytes);
        searchProvider.GetNextFilename = reasonWhyLastFileNotCompatible =>
            GetNextRomFileToTry?.Invoke(reasonWhyLastFileNotCompatible) ?? null;

        return searchProvider.SearchAndReadFromCompatibleRom(initialRomFile: Project.AttachedRomFilename);
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