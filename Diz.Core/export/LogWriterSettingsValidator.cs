using System;
using System.IO;
using Diz.Core.util;
using FluentValidation;
using LightInject;

namespace Diz.Core.export
{
    public class LogWriterSettingsValidator : AbstractValidator<LogWriterSettings>
    {
        public LogWriterSettingsValidator()
        {
            When(x => x.OutputToString, () =>
                    Include(new LogWriterSettingsOutputString()))
                .Otherwise(() =>
                    Include(new LogWriterSettingsOutputMultipleFiles()));
        }
    }

    public class LogWriterSettingsOutputString : AbstractValidator<LogWriterSettings>
    {
        public LogWriterSettingsOutputString()
        {
            // runs when OutputToString == true

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
        private static IFilesystemService Fs => Service.Container.GetInstance<IFilesystemService>();
        
        private static bool DirectoryExists(string path) =>
            Fs.DirectoryExists(Path.GetDirectoryName(path));

        // this is not the most bulletproof thing in the world.
        // it's hard to validate without hitting the disk, you should follow this with additional checks
        private static bool PathLooksLikeDirectoryNameOnly(string fileOrFolderPath) =>
            Path.GetFileName(fileOrFolderPath) == string.Empty ||
            !Path.HasExtension(fileOrFolderPath);

        // runs when OutputToString == false
        public LogWriterSettingsOutputMultipleFiles()
        {
            RuleFor(x => x.FileOrFolderOutPath)
                .NotEmpty()
                .WithMessage("No file path set")
                .Must(DirectoryExists)
                .WithMessage("Directory doesn't exist");

            // verify what we have appears to be a filename and NOT a directory
            RuleFor(x => x.FileOrFolderOutPath)
                .Must(PathLooksLikeDirectoryNameOnly)
                .When(settings => settings.Structure == LogWriterSettings.FormatStructure.OneBankPerFile)
                .WithMessage("Output directory doesn't appear to be a valid");
        }
    }
}