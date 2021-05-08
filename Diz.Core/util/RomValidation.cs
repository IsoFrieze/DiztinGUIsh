#nullable enable

using System;
using Diz.Core.model;
using Diz.Core.serialization.xml_serializer;
using FluentValidation;

namespace Diz.Core.util
{
    public class AddRomDataCommand
    {
        public byte[]? RawBytesToAdd { get; set; }
        public bool ShouldProjectCartTitleMatchRomBytes { get; set; } = true;
        public ProjectXmlSerializer.Root? Root { get; set; }
        public Project? Project => Root?.Project;
        public Func<string, string>? GetNextRomFileToTry { get; set; }
        public MigrationRunner MigrationRunner { get; set; }

        private bool Prep()
        {
            if (Project == null)
                return false;

            MigrationRunner?.OnLoadingBeforeAddLinkedRom(this);
            return true;
        }
        
        public bool TryReadAttachedProjectRom()
        {
            if (!Prep()) 
                return false;

            if (!RetrieveBytes()) 
                return false;

            FinishPopulatingBytes();
            return true;
        }
        
        private void FinishPopulatingBytes()
        {
            Project.Data.CopyRomDataIn(RawBytesToAdd);
            MigrationRunner?.OnLoadingAfterAddLinkedRom(this);
        }

        private bool RetrieveBytes()
        {
            // special case, use mostly for testing.
            if (HandleUnusualOverrideBytes()) 
                return true;

            // normal case: keep asking user for ROMS that meet our criteria, or give up
            Project.AttachedRomFilename = SearchForValidRom();
            return Project.AttachedRomFilename != null;
        }

        private bool HandleUnusualOverrideBytes()
        {
            RawBytesToAdd = Project.Data.GetOverriddenRomBytes();
            return RawBytesToAdd != null;
        }

        private string? SearchForValidRom()
        {
            // try to open a ROM that matches us, if not, ask the user until they give up
            var nextFileToTry = Project!.AttachedRomFilename;

            do
            {
                if (nextFileToTry == null || AttemptToLinkRomToProject(ref nextFileToTry))
                    break;
                
            } while (true);

            return nextFileToTry;
        }

        // if true and nextFileToTry is not null, call this function again to try again. otherwise, give up.
        private bool AttemptToLinkRomToProject(ref string? nextFileToTry)
        {
            var errors = ReadRomIfMatchesProject(nextFileToTry!);
            if (errors == null) 
                return true;
            
            nextFileToTry = GetNextRomFileToTry?.Invoke(errors); // prompt user with errors / next steps / ask for a new ROM
            return nextFileToTry != null;
        }

        public string? ReadRomIfMatchesProject(string filename)
        {
            var errors = DoReadRomIfMatchesProject(filename);
            if (errors != null)
                RawBytesToAdd = null; // reset

            return errors;
        }

        public string? DoReadRomIfMatchesProject(string filename)
        {
            try
            {
                RawBytesToAdd = RomUtil.ReadAllRomBytesFromFile(filename);
                return RawBytesToAdd == null 
                    ? $"Can't add rom data, none able to be read from disk. {nameof(RawBytesToAdd)}" 
                    : Validate();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public string? Validate()
        {
            var validator = new AddRomDataCommandValidator();
            var results = validator.Validate(this);
            return !results.IsValid ? results.ToString() : null;
        }
    }


    public class AddRomDataCommandValidator : AbstractValidator<AddRomDataCommand>
    {
        public AddRomDataCommandValidator()
        {
            RuleFor(x => x.Project).NotEmpty();
            RuleFor(x => x.RawBytesToAdd).NotEmpty();
            RuleFor(x => x.Project).Must(HaveValidRomSize);
            RuleFor(x => x.Project).Must(MatchChecksums);
            
            RuleFor(x => x.Project).Must(MatchCartTitle)
                .When(x => x.ShouldProjectCartTitleMatchRomBytes);
        }

        private static bool MatchChecksums(AddRomDataCommand cmd, Project project,
            ValidationContext<AddRomDataCommand> validationContext)
        {
            var checksumToVerify =
                ByteUtil.ConvertByteArrayToInt32(cmd.RawBytesToAdd, project.Data.RomComplementOffset);
            if (checksumToVerify == project.InternalCheckSum)
                return true;

            validationContext.AddFailure($"The linked ROM's checksums '{checksumToVerify:X8}' " +
                                         $"doesn't match the project's checksums of '{project.InternalCheckSum:X8}'.");
            return false;
        }

        private static bool HaveValidRomSize(AddRomDataCommand cmd, Project project,
            ValidationContext<AddRomDataCommand> validationContext)
        {
            if (cmd.RawBytesToAdd?.Length > project.Data.RomSettingsOffset + 10)
                return true;

            validationContext.AddFailure("The linked ROM is too small. It can't be opened.");
            return false;
        }

        private static bool MatchCartTitle(AddRomDataCommand cmd, Project project,
            ValidationContext<AddRomDataCommand> validationContext)
        {
            var gameNameFromRomBytes = RomUtil.GetCartridgeTitleFromRom(cmd.RawBytesToAdd, project.Data.RomSettingsOffset);
            var requiredGameNameMatch = project.InternalRomGameName;

            return MatchCartTitle(requiredGameNameMatch, gameNameFromRomBytes, validationContext);
        }

        private static bool MatchCartTitle(string requiredGameNameMatch, string gameNameFromRomBytes,
            ValidationContext<AddRomDataCommand> validationContext)
        {
            if (requiredGameNameMatch == gameNameFromRomBytes)
                return true;

            validationContext.AddFailure(
                $"Verification check: The project file requires the linked ROM's SNES header\n" +
                $"to have a cartridge title name of:\n'{requiredGameNameMatch}'.\n" +
                $"But, this doesn't match the title in the ROM file, which is:\n'{gameNameFromRomBytes}'.");
            return false;
        }
    }
}