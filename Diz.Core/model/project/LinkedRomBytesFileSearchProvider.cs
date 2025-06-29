#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.model.project;

public interface IReadFromFileBytes
{
    byte[] ReadRomFileBytes(string filename);
}

public class ReadFromFileBytes : IReadFromFileBytes
{
    public byte[] ReadRomFileBytes(string filename) => 
        RomUtil.ReadRomFileBytes(filename);
}

public delegate string? FilenameGetter(string reasonLastFailed);
public delegate void CompatibilityEnforcer(string filename, byte[] fileContents);
    
public interface ILinkedRomBytesProvider
{
    // caller should throw InvalidDataException if it's not compatible
    public CompatibilityEnforcer? EnsureCompatible { get; set; }
        
    // caller will be prompted to provide another filename to try, or return null if search should end
    public FilenameGetter? GetNextFilename { get; set; }
        
    (string filename, byte[] romBytes)? SearchAndReadFromCompatibleRom(string initialRomFile, string otherPromptText = "");
}
    
[UsedImplicitly]
public class LinkedRomBytesFileSearchProvider : ILinkedRomBytesProvider
{
    private readonly IReadFromFileBytes romBytesReader;
        
    public LinkedRomBytesFileSearchProvider(IReadFromFileBytes romBytesReader)
    {
        this.romBytesReader = romBytesReader;
    }

    public CompatibilityEnforcer? EnsureCompatible { get; set; }
    public FilenameGetter? GetNextFilename { get; set; }

    public (string filename, byte[] romBytes)? SearchAndReadFromCompatibleRom(string? initialRomFile, string otherPromptText = "")
    {
        var candidateFile = initialRomFile;
            
        while (candidateFile != null)
        {
            var bytes = GetRomFileContentsIfCompatible(candidateFile, out var lastFailureReason);
            if (bytes != null)
            {
                Debug.Assert(string.IsNullOrEmpty(lastFailureReason));
                return (candidateFile, bytes);
            }

            var promptText = $"Project:'{otherPromptText.Trim()}'\n{lastFailureReason}";
            candidateFile = GetNextFilename?.Invoke(promptText);
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

    private byte[]? GetRomFileBytesOrFailReason(string romFilename, out string reasonFailed)
    {
        reasonFailed = "";

        if (romFilename == "")
        {
            // you can hit this when upgrading from older project files to newer ones which don't store the 
            // ROM file directly (they store it in the prefs). this is OK and somewhat expected.
            reasonFailed = "ROM file not yet specified in project data.\nThis is expected if opening a project for the first time, or, upgrading Diz.\nPlease Link a new ROM file to continue.";
            return null;
        }
        
        try
        {
            return romBytesReader.ReadRomFileBytes(romFilename);
        }
        catch (Exception ex)
        {
            reasonFailed = ex.Message;
            return null;
        }
    }
}