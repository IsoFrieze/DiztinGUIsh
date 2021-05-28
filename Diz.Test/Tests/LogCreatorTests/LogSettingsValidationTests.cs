using System;
using System.IO;
using System.Net.Security;
using Diz.Core.export;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace Diz.Test.Tests.LogCreatorTests
{
    public sealed class LogSettingsValidationTests
    {
        internal class LogWriterSettingsTestValidator : LogWriterSettingsValidator
        {
            protected override AbstractValidator<LogWriterSettings> MultiValidator => new MultiValidateOverride();
        }

        internal class MultiValidateOverride : LogWriterSettingsOutputMultipleFiles
        {
            public const string ThisDirExists = @"c:\exists";

            public override bool DirectoryExists(string path) => 
                Path.GetDirectoryName(path) == ThisDirExists;
        }
        
        private static void Run(LogWriterSettings settings, bool shouldThrow)
        {
            var validateShould = new LogWriterSettingsTestValidator()
                .Invoking(x => x.ValidateAndThrow(settings)).Should();

            if (shouldThrow)
                validateShould.Throw<ValidationException>();
            else
                validateShould.NotThrow();
        }

        private static void ShouldThrow(LogWriterSettings settings) => Run(settings, true);
        private static void ShouldNotThrow(LogWriterSettings settings) => Run(settings, false);

        [Fact]
        public void TestHarness()
        {
            var v = new MultiValidateOverride();
            v.DirectoryExists(PretendExists()).Should().Be(true);
            v.DirectoryExists(PretendNoExists()).Should().Be(false);
        }
        
        private static string PretendExists(string filename = "junk.asm") => 
            Path.Combine(MultiValidateOverride.ThisDirExists, filename);
        
        private static string PretendNoExists(string filename = "junk.asm") => 
            Path.Combine("d:\\not_exists\\", filename);

        [Fact]
        public void TestSettingsValidationForFile()
        {
            var outFile = new LogWriterSettings
            {
                OutputToString = false, 
                Structure = LogWriterSettings.FormatStructure.SingleFile
            };
            
            ShouldThrow(outFile with {FileOrFolderOutPath = ""});
            ShouldThrow(outFile with {FileOrFolderOutPath = PretendNoExists()});
            ShouldNotThrow(outFile with {FileOrFolderOutPath = PretendExists()});

            outFile = outFile with
            {
                Structure = LogWriterSettings.FormatStructure.OneBankPerFile
            };
            
            ShouldThrow(outFile with {FileOrFolderOutPath = ""});
            ShouldThrow(outFile with {FileOrFolderOutPath = PretendNoExists()});
            ShouldThrow(outFile with {FileOrFolderOutPath = PretendExists()});
        }

        [Fact]
        private static void TestLooksLikeDir()
        {
            // this.. could be a bit janky. if you hit stuff here, it's probably not you, it's probably us.
            
            var fullPath = PretendExists();
            LogWriterSettingsOutputMultipleFiles.PathLooksLikeDirectoryNameOnly(fullPath).Should().BeFalse();
            LogWriterSettingsOutputMultipleFiles.PathLooksLikeDirectoryNameOnly("c:\\whatever").Should().BeTrue();
            LogWriterSettingsOutputMultipleFiles.PathLooksLikeDirectoryNameOnly("c:\\whatever\\").Should().BeTrue();
        }

        [Fact]
        public void TestSettingsValidationForString()
        {
            var outFile = new LogWriterSettings
            {
                OutputToString = true, 
                Structure = LogWriterSettings.FormatStructure.SingleFile
            };
            ShouldNotThrow(outFile);
            ShouldNotThrow(outFile with {FileOrFolderOutPath = ""});
            ShouldThrow(outFile with {FileOrFolderOutPath = PretendNoExists()});
            ShouldThrow(outFile with {FileOrFolderOutPath = PretendExists()});

            outFile = outFile with
            {
                Structure = LogWriterSettings.FormatStructure.OneBankPerFile
            };
            ShouldThrow(outFile with {FileOrFolderOutPath = ""});
            ShouldThrow(outFile with {FileOrFolderOutPath = PretendNoExists()});
            ShouldThrow(outFile with {FileOrFolderOutPath = PretendExists()});
        }

        [Fact]
        private static void TestDirect()
        {
            var outFile = new LogWriterSettings
            {
                OutputToString = false, 
                Structure = LogWriterSettings.FormatStructure.SingleFile
            };
            
            var validationFailures = new LogWriterSettingsTestValidator().Validate(outFile with {FileOrFolderOutPath = ""});
            validationFailures.IsValid.Should()
                .BeFalse("should have failed the check", validationFailures.ToString());
        }
    }
}