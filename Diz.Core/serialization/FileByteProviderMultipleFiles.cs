#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Diz.Core.serialization;

public class FileByteProviderMultipleFiles : IFileByteProvider
{
    public byte[] ReadAllBytes(string filename)
    {
        if (!filename.EndsWith(".dizdir"))
        {
            // Not a special file, read all bytes normally and we're done!
            return File.ReadAllBytes(filename);
        }

        return ReadMultipleSaveFiles(filename);
    }

    private static byte[] ReadMultipleSaveFiles(string filename)
    {
        // Special file found, parse contents.
        var contents = File.ReadAllText(filename);
        if (!contents.StartsWith("DIZ-MULTIFILE:") || !contents.EndsWith("version=1"))
        {
            throw new Exception("Invalid file format!");
        }

        // Get the directory with the same name as the file (without extension)
        var baseFilenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
        var directoryName = Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty, baseFilenameWithoutExtension);

        // Regular expression to exactly match our file name pattern
        var regex = new Regex(@"^\d{5}_save.*\.txt$");

        // Get all files from the directory.
        var allFiles = Directory.GetFiles(directoryName);
    
        // Throw an error if there are files not matching the pattern.
        if (allFiles.Any(file => !regex.IsMatch(Path.GetFileName(file))))
        {
            throw new Exception("There are files that do not match the required pattern!");
        }

        // Filter files to match our pattern and order them.
        var matchedFiles = allFiles.Where(path => regex.IsMatch(Path.GetFileName(path)))
            .OrderBy(f => f)
            .ToList();

        // Read all files in the correct order and concatenate their bytes.
        var result = new List<byte>();
        foreach (var fileBytes in matchedFiles.Select(File.ReadAllBytes))
        {
            result.AddRange(fileBytes);
        }

        return result.ToArray();
    }

    public void WriteBytes(string filename, byte[] data)
    {
        // Make a tmp dir for output
        var baseFilenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
        var directoryName = Path.GetDirectoryName(filename) ?? string.Empty;
        var tempDirPath = Path.Combine(directoryName, baseFilenameWithoutExtension + ".tmp");
        if (Directory.Exists(tempDirPath))
            Directory.Delete(tempDirPath, true);
        Directory.CreateDirectory(tempDirPath);

        // read the bytes we're supposed to write (originally to just one file, but..)
        var content = Encoding.UTF8.GetString(data);
        if (content == null)
            throw new InvalidOperationException("Content cannot be null.");

        // instead, split the output into a bunch different files in one directory 
        new FileSplitWriter(tempDirPath).OutputSplitFiles(content);

        // Rename the .tmp dir to the final directory
        var previousDir = Path.Combine(directoryName, baseFilenameWithoutExtension);
        if (Directory.Exists(previousDir))
            Directory.Delete(previousDir, true);
        Directory.Move(tempDirPath, previousDir);

        // Finally, last step: the original file gets replaced with a tag to let us know it's a multi-file
        File.WriteAllText(filename, "DIZ-MULTIFILE:version=1");
    }
}

public class FileSplitWriter
{
    public int FileCount { get; private set; } = -1;

    protected StreamWriter? Sw = null;
    public string OutputDir { get; }

    public FileSplitWriter(string outputDir)
    {
        OutputDir = outputDir;
    }

    public void OutputSplitFiles(string content)
    {
        // super-hacky stringsplit. we are NOT parsing XML, just splitting up into files whenever we see any of the following.
        // the entire point of this is for better git merging, so, optimize for that use case.
        // this is a little brittle and specific to the Diz2.0 file format as of 2023. if it gives you any trouble, don't rely on it.
        
        var tokens = new List<string>
        {
            "<Data",
            "<Comments",
            "<Labels",
            "<RomBytes",
            "\r\n;pos=",    // we'll include the newline here to make it easier when merging later.
                            // otherwise, a newline would be required at the END of each text file fragment and if removed would blow things up.
                            // a newline will still be required at the top of the file, but, if removed it'll happen to work out.
            "</RomBytes>"
        };

        // Step 1: Find indices of every token
        var tokenPositions = new List<(int index, string token)>();
        foreach (var token in tokens)
        {
            var index = 0;

            while ((index = content.IndexOf(token, index, StringComparison.Ordinal)) != -1)
            {
                tokenPositions.Add((index, token));
                index += token.Length;
            }
        }

        // Sort our found positions
        tokenPositions = tokenPositions.OrderBy(item => item.index).ToList();

        // Step 2: Output each range to a separate file
        var lastIndex = 0;
        SwitchToNewOutputFile("");

        for (var i = 0; i < tokenPositions.Count; i++)
        {
            var endIndex = tokenPositions[i].index;
            Sw!.Write(content[lastIndex..endIndex]);

            var token = tokenPositions[i].token;
            lastIndex = endIndex + token.Length;

            var filePostfix = SanitizeFileChars(token);
            SwitchToNewOutputFile(filePostfix);

            Sw!.Write(token);
        }

        // Write remaining content to the last file, if any
        if (lastIndex < content.Length)
        {
            Sw!.Write(content[lastIndex..]);
        }

        if (Sw == null)
            return;

        Sw.Flush();
        Sw.Close();
        Sw = null;
    }

    private static string SanitizeFileChars(string nextToken) =>
        Regex.Replace(nextToken, "[^a-zA-Z0-9_.]", "");

    private void SwitchToNewOutputFile(string filePostfix = "")
    {
        if (Sw != null)
        {
            Sw.Flush();
            Sw.Close();
        }

        FileCount++;
        var fullFilename = CreateFullFilename(filePostfix);
        Sw = new StreamWriter(fullFilename);
    }

    private string CreateFullFilename(string filePostfix)
    {
        var finalPostfix = filePostfix.Length > 0 ? $"_{filePostfix}" : "";
        return Path.Combine(OutputDir, $"{FileCount:D5}_save{finalPostfix}.txt");
    }
}