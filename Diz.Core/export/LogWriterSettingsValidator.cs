using System.IO;
using Diz.Core.util;
using FluentValidation;

namespace Diz.Core.export
{
    public class LogWriterSettingsValidator : AbstractValidator<LogWriterSettings>
    {
        public LogWriterSettingsValidator(IFilesystemService fs)
        {
            When(x => x.OutputToString, () =>
                    Include(new LogWriterSettingsOutputString()))
                .Otherwise(() =>
                    Include(new LogWriterSettingsOutputMultipleFiles(fs)));
        }
    }

    public class LogWriterSettingsOutputString : AbstractValidator<LogWriterSettings>
    {
        public LogWriterSettingsOutputString()
        {
            // runs when OutputToString == true
            // i.e. when we expect the output to be a single .asm file, and not a directory

            RuleFor(x => x.Structure)
                .NotEqual(LogWriterSettings.FormatStructure.OneBankPerFile)
                .WithMessage("Can't use one-bank-per-file output with string output enabled");

            RuleFor(x => x.FileOrFolderOutPath)
                .Empty()
                .WithMessage("Can't use one-bank-per-file output with valid file or path specified");
        }
    }
    
    public class LogWriterSettingsOutputMultipleFiles : AbstractValidator<LogWriterSettings>
    {
        private readonly IFilesystemService fs;
        
        private bool OutputDirReallyExistsOnDisk(LogWriterSettings settings)
        {
            var path = settings.BuildFullOutputPath();
            return fs.DirectoryExists(Path.GetDirectoryName(path));
        }

        // this is not the most bulletproof thing in the world.
        // it's hard to validate without hitting the disk, you should follow this with additional checks
        private bool PathLooksLikeDirectoryNameOnly(string fileOrFolderPath) =>
            Path.GetFileName(fileOrFolderPath) == string.Empty ||
            !Path.HasExtension(fileOrFolderPath);
        
        public LogWriterSettingsOutputMultipleFiles(IFilesystemService fs)
        {
            // runs when OutputToString == false
            // i.e. we expect the output path to be a directory path and not a file.
            
            this.fs = fs;
            
            RuleFor(x => x.FileOrFolderOutPath)
                .NotEmpty().WithMessage("Disassembly output file directory is empty, but is required");
                
            RuleFor(settings => settings)
                .Must(OutputDirReallyExistsOnDisk).WithMessage("Disassembly output directory doesn't exist on disk.");

            // verify what we have appears to be a filename and NOT a directory
            RuleFor(x => x.FileOrFolderOutPath)
                .Must(PathLooksLikeDirectoryNameOnly)
                .When(settings => settings.Structure == LogWriterSettings.FormatStructure.OneBankPerFile)
                .WithMessage("Disassembly output directory doesn't appear to be a valid directory name");
        }
    }
}