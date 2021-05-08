#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using Diz.Core.util;

namespace Diz.Core.model.project
{
    public delegate string? FilenameGetter(string reasonLastFailed);
    public delegate void CompatibilityEnforcer(string filename, byte[] fileContents);
    
    internal interface ILinkedRomBytesProvider
    {
        // caller should throw InvalidDataException if it's not compatible
        public CompatibilityEnforcer? EnsureCompatible { get; set; }
        
        // caller will be prompted to provide another filename to try, or return null if search should end
        public FilenameGetter? GetNextFilename { get; set; }
        
        (string filename, byte[] romBytes)? SearchAndReadFromCompatibleRom(string initialRomFile);
    }
    
    public class LinkedRomBytesFileSearchProvider : ILinkedRomBytesProvider
    {
        public CompatibilityEnforcer? EnsureCompatible { get; set; }
        public FilenameGetter? GetNextFilename { get; set; }

        public (string filename, byte[] romBytes)? SearchAndReadFromCompatibleRom(string? initialRomFile)
        {
            var candidateFile = initialRomFile;
            
            while (candidateFile != null)
            {
                var bytes = GetRomFileContentsIfCompatible(candidateFile, out string lastFailureReason);
                if (bytes != null)
                {
                    Debug.Assert(string.IsNullOrEmpty(lastFailureReason));
                    return (candidateFile, bytes);
                }

                candidateFile = GetNextFilename?.Invoke(lastFailureReason);
            }

            return null;
        }

        private byte[]? GetRomFileContentsIfCompatible(string romFilename, out string reasonFailed)
        {
            reasonFailed = "";

            var bytes = GetRomFileBytesOrFailReason(romFilename, out reasonFailed);
            if (bytes == null)
                return null;

            try
            {
                EnsureCompatible?.Invoke(romFilename, bytes);
                return bytes;
            }
            catch (InvalidDataException ex)
            {
                reasonFailed = ex.Message;
                return null;
            }
        }

        private static byte[]? GetRomFileBytesOrFailReason(string romFilename, out string reasonFailed)
        {
            reasonFailed = "";
            try
            {
                return RomUtil.ReadRomFileBytes(romFilename);
            }
            catch (Exception ex)
            {
                reasonFailed = ex.Message;
                return null;
            }
        }
    }
}