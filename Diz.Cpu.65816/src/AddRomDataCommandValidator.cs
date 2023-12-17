#nullable enable

using Diz.Core.model;
using Diz.Core.util;
using FluentValidation;

namespace Diz.Cpu._65816;

public class RomToProjectAssociation
{
    public byte[]? RomBytes;
    public Project? Project;
}
    
public class AddRomDataCommandValidator : AbstractValidator<RomToProjectAssociation>
{
    public bool EnsureProjectAndRomCartTitleMatch { get; set; } = true;

    public AddRomDataCommandValidator()
    {
        RuleFor(x => x.Project).NotEmpty();
        RuleFor(x => x.Project!.Data).NotEmpty();
        RuleFor(x => x.RomBytes).NotEmpty();
        RuleFor(x => x.Project).Must(HaveValidRomSize);
        RuleFor(x => x.Project).Must(MatchChecksums);
        RuleFor(x => x.Project).Must(MatchCartTitle);
    }
        
    private static bool MatchChecksums(RomToProjectAssociation container, Project? project,
        ValidationContext<RomToProjectAssociation> validationContext)
    {
        if (project == null) 
            throw new ArgumentNullException(nameof(project));

        var snesData = project.Data.GetSnesApi() ?? throw new ArgumentNullException();
        var checksumToVerify =
            ByteUtil.ConvertByteArrayToUInt32(container.RomBytes, snesData.RomComplementOffset);
            
        if (checksumToVerify == project.InternalCheckSum)
            return true;

        validationContext.AddFailure($"The linked ROM's checksums '{checksumToVerify:X8}' " +
                                     $"doesn't match the project's checksums of '{project.InternalCheckSum:X8}'.");
        return false;
    }

    private static bool HaveValidRomSize(RomToProjectAssociation container, Project? project, ValidationContext<RomToProjectAssociation> validationContext)
    {
        var snesData = project?.Data.GetSnesApi();
        if (snesData == null)
            return false;
        
        if (container.RomBytes?.Length > snesData.RomSettingsOffset + 10)
            return true;

        validationContext.AddFailure("The linked ROM is too small. It can't be opened.");
        return false;
    }

    private bool MatchCartTitle(RomToProjectAssociation container, Project? project,
        ValidationContext<RomToProjectAssociation> validationContext)
    {
        if (!EnsureProjectAndRomCartTitleMatch)
            return true;
            
        var snesData = project?.Data.GetSnesApi();
        if (project == null || snesData == null)
            return false;
            
        var gameNameFromRomBytes = RomUtil.GetCartridgeTitleFromRom(container.RomBytes, snesData.RomSettingsOffset);
        var requiredGameNameMatch = project.InternalRomGameName;

        return MatchCartTitle(requiredGameNameMatch, gameNameFromRomBytes, validationContext);
    }

    private static bool MatchCartTitle(string requiredGameNameMatch, string gameNameFromRomBytes,
        ValidationContext<RomToProjectAssociation> validationContext)
    {
        if (requiredGameNameMatch == gameNameFromRomBytes)
            return true;

        validationContext.AddFailure(
            $"Verification check: The project file requires the linked ROM's SNES header " +
            $"to have a cartridge title name of:'{requiredGameNameMatch}'. \n" +
            $"But, this doesn't match the title in the ROM file, which is:\n'{gameNameFromRomBytes}'.");
        return false;
    }
}