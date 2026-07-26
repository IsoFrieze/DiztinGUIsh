#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Diz.Core.util;

/*
 * TODO:
 * Couple things for ongoing refactors:
 * 1) This class should ideally live with the Diz.LogWriter project (not Diz.Core)
 * 2) Probably use dependency injection starting with this system to register settings providers in Diz like this one?
 */

namespace Diz.Core.export;


public interface ILogWriterSettings
{
}

public record LogWriterSettings : ILogWriterSettings
{
    // path to output file or folder
    public const string DefaultStr = "%label:-22% %code:37%;%pc%|%bytes%|%ia%; %comment%";
        
    public enum FormatUnlabeled
    {
        ShowAll = 0,
        ShowInPoints = 1, // TODO Add Show In Points with +/- labels
        ShowNone = 2
    }

    public enum FormatStructure
    {
        SingleFile = 0,
        OneBankPerFile = 1
    }

    public string Format { get; init; } = DefaultStr;
    public int DataPerLine { get; init; } = 8;
    public FormatUnlabeled Unlabeled { get; init; } = FormatUnlabeled.ShowInPoints;
    public FormatStructure Structure { get; init; } = FormatStructure.OneBankPerFile;
    
    // tmp hack until we fix single file mode. allows sample data to still be generated
    [XmlIgnore] public bool SuppressSingleFileModeDisabledError { get; init; } = false;
    
    public bool NewLine { get; init; } = false;
    public bool OutputExtraWhitespace  { get; init; } = true;
    public bool GenerateFullLine { get; init; } = true;
    public bool IncludeUnusedLabels  { get; init; }
    public bool PrintLabelSpecificComments { get; init; }
    public bool GeneratePlusMinusLabels { get; init; } = true;

    // this is an experimental option, if useful, remove [XmlIgnore] and add the UI for this
    [XmlIgnore] public bool AppendFlagTypeToComment { get; init; } = false;

        
    /// <summary>
    /// specify an override for the # of bytes to assemble. default is to visit every byte in the entire ROM 
    /// </summary>
    public int RomSizeOverride { get; init; } = -1;

    /// <summary>
    /// The (usually absolute) base path to the project directory, if any.
    /// Don't save this with the project XML.
    /// </summary>
    [XmlIgnore]
    public string? BaseOutputPath { get; init; }
        
    /// <summary>
    /// Relative path to add on after the base path. This is the GENERATED tier: everything
    /// Diz writes (.asm, asset manifests) lands here and is rewritten on every export.
    /// </summary>
    public string FileOrFolderOutPath { get; init; } = "generated";

    // ----------------------------------------------------------------------------------------
    // Directory tiers of the exported repo, relative to BaseOutputPath (the repo root). Diz
    // writes into none of these -- it emits them into build.ninja and into the assembly's
    // incbin paths, so a project whose repo uses different names stays buildable.
    //
    // The full tier set is:
    //   <FileOrFolderOutPath>/  generated  - Diz output (.asm + assets/**.json manifests)
    //   AssetsDirPath/          assets     - hand-authored inputs (character tables, etc.)
    //   ExtractedDirPath/       extracted  - build output of the `extract` step, editable
    //   BuildDirPath/           build      - compiled artifacts (assets/**.bin, the ROM)
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Hand-authored asset layer: tracked human-owned inputs, never written by export. It is
    /// the last (and always-complete) layer of the asset override search path.
    /// </summary>
    public string AssetsDirPath { get; init; } = "assets";

    /// <summary>
    /// Where the build decodes ROM bytes into editable sources (PNG/YAML/BRR). Derived, and
    /// re-creatable at any time from the ROM plus the manifests, so it is never hand-edited.
    /// </summary>
    public string ExtractedDirPath { get; init; } = "extracted";

    /// <summary>
    /// Where the build writes compiled artifacts. Assembly directives incbin
    /// <c>&lt;BuildDirPath&gt;/assets/&lt;name&gt;.bin</c>, never the editable source.
    /// </summary>
    public string BuildDirPath { get; init; } = "build";

    public bool OutputToString { get; init; }
    public string ErrorFilename { get; init; } = "errors.txt";

    // ----------------------------------------------------------------------------------------
    // Exclude-labels-by-author blocklist.
    //
    // A label whose Author is in this set is FULLY hidden from export (label listing AND operand
    // naming). Matching is case-insensitive. Empty (the default) = nothing excluded.
    //
    // IMPORTANT: this is stored internally as a single normalized string (excludedLabelAuthors)
    // rather than a collection field. Two reasons:
    //   1) value-equality: LogWriterSettings is a record, and ProjectController.UpdateExportSettings
    //      uses .Equals to detect changed settings. Record equality compares FIELDS -- a collection
    //      field would compare by REFERENCE, so two blocklists with identical contents would look
    //      "changed". A string field compares by content, which is exactly what we want.
    //   2) serialization: the interface-typed ExcludedLabelAuthors is [XmlIgnore]; the normalized
    //      string ExcludedLabelAuthorsList is what serializes with the project (persists the
    //      blocklist). ExtendedXmlSerializer can't serialize an IReadOnlyCollection<string> member.
    private readonly string excludedLabelAuthors = "";

    // splits on commas -- see NormalizeAuthors for why that's safe as the internal delimiter.
    private static readonly char[] AuthorSeparators = [','];

    /// <summary>
    /// Authors whose labels are fully hidden from export (case-insensitive). Computed view over
    /// the normalized backing string. NOT serialized directly (interface-typed, and would duplicate
    /// state) -- ExcludedLabelAuthorsList is the persisted form.
    /// </summary>
    [XmlIgnore]
    public IReadOnlyCollection<string> ExcludedLabelAuthors
    {
        get => excludedLabelAuthors.Length == 0
            ? Array.Empty<string>()
            : excludedLabelAuthors.Split(AuthorSeparators, StringSplitOptions.RemoveEmptyEntries);
        init => excludedLabelAuthors = NormalizeAuthors(value);
    }

    /// <summary>
    /// Persisted (and value-equality-participating) form of ExcludedLabelAuthors: the normalized
    /// blocklist as a single comma-joined string. Normalization = trim, drop blanks, de-duplicate
    /// case-insensitively, sort (OrdinalIgnoreCase). Serialized with the project.
    /// </summary>
    public string ExcludedLabelAuthorsList
    {
        get => excludedLabelAuthors;
        init => excludedLabelAuthors = NormalizeAuthors(
            (value ?? "").Split(AuthorSeparators, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeAuthors(IEnumerable<string>? authors) =>
        string.Join(",", (authors ?? Enumerable.Empty<string>())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase));

    public LogWriterSettings WithPathRelativeTo(string newFileNameAndPath, string? pathToMakeRelativeTo) =>
        this with
        {
            FileOrFolderOutPath = Util.TryGetRelativePath(newFileNameAndPath, pathToMakeRelativeTo),
            BaseOutputPath = pathToMakeRelativeTo,
        };

    public string BuildFullOutputPath()
    {
        // this is still a bit of an in-progress mess. sigh.
        
        var path = FileOrFolderOutPath;
        if (Structure == FormatStructure.OneBankPerFile)
            path += "\\"; // force it to treat it as a path.

        // if it's absolute path, use that first, ignore base path
        if (Path.IsPathFullyQualified(path))
            return path;

        // if it's not an absolute path, combine BaseOutputPath and FileOrFolderPath to get the final
        var relativeFolderPath = Path.GetDirectoryName(path) ?? "";
        
        if (Structure == FormatStructure.OneBankPerFile)
            relativeFolderPath += "\\"; // force it to treat it as a path.

        return Path.Combine(BaseOutputPath ?? "", relativeFolderPath);
    }
    
    public string? Validate(IFilesystemService fs)
    {
        var results = new LogWriterSettingsValidator(fs).Validate(this);
        return !results.IsValid ? results.ToString() : null;
    }

    public bool IsValid(IFilesystemService fs) => Validate(fs) == null;
}