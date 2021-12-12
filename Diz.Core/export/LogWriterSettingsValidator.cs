using System.IO;
using FluentValidation;

namespace Diz.Core.export
{
    public class LogWriterSettingsValidator : AbstractValidator<LogWriterSettings>
    {
        protected virtual AbstractValidator<LogWriterSettings> MultiValidator => 
            new LogWriterSettingsOutputMultipleFiles();
        
        protected virtual AbstractValidator<LogWriterSettings> StringValidator => 
            new LogWriterSettingsOutputString();

        public LogWriterSettingsValidator()
        {
            void ValidateFileOutput() => Include(MultiValidator);
            void ValidateStringOutput() => Include(StringValidator);

            When(x => x.OutputToString, ValidateStringOutput).Otherwise(ValidateFileOutput);
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
        // protected virtual string GetFileName(string path) => Path.GetFileName(path);
        public virtual bool DirectoryExists(string path) => 
            Directory.Exists(Path.GetDirectoryName(path));

        // this is not the most bulletproof thing in the world.
        // it's hard to validate without hitting the disk, you should follow this with additional checks
        public static bool PathLooksLikeDirectoryNameOnly(string fileOrFolderPath) =>
            Path.GetFileName(fileOrFolderPath) == string.Empty || 
            !Path.HasExtension(fileOrFolderPath);
        
        public LogWriterSettingsOutputMultipleFiles()
        {
            // runs when OutputToString == false

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