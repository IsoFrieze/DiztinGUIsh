#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Annotations;

namespace Diz.Core.model.project;

/// <summary>
/// A machine-global, user-local registry mapping a project's ROM identity
/// (internal checksum + internal cartridge game name) to a full path to a ROM file on disk.
///
/// Background: the authoritative, per-project place to record where a project's ROM lives is the
/// project's own user-prefs file (its AttachedRomFilename). That file is deliberately kept out of
/// source control (it holds a machine-local absolute path), so a freshly-cloned project - or one
/// checkout of it in each of several worktrees - won't have one. Rather than re-locating the ROM by
/// hand in every such checkout, Diz can consult this single shared registry, which lives outside any
/// project directory and so is visible to every Diz instance on the machine.
///
/// The registry is only ever an untrusted HINT. Every path it hands back is still run through the
/// normal ROM-vs-project verification before use - but that verification is HEADER-ONLY: it compares
/// the 4 ROM-header complement/checksum bytes and (only when enabled) the cartridge title, and never
/// hashes ROM contents. So a DIFFERENT ROM that happens to share those header fields - e.g. an
/// IPS-patched copy whose header checksum wasn't recomputed - would pass verification and be attached.
/// Entries are a convenience hint for locating a likely file, not a guarantee of the exact ROM.
/// </summary>
public interface IGlobalRomRegistry
{
    /// <summary>
    /// Full path to the registry file on disk. May not exist yet.
    /// </summary>
    string RegistryFilePath { get; }

    /// <summary>
    /// Best-effort: the recorded ROM path(s) for a ROM identity. At most one path is stored per
    /// identity - Remember keeps a single path each - so there is no ranking or ordering to the result.
    /// Never throws; returns empty on any problem (missing/unreadable file, or a schema version we
    /// don't understand). Every returned path is an UNVERIFIED hint - the caller must still verify it.
    /// </summary>
    IEnumerable<string> FindCandidateRomPaths(uint internalCheckSum, string internalRomGameName);

    /// <summary>
    /// Record (insert or update) a known-good ROM path for a ROM identity, so a later lookup - e.g.
    /// from a different worktree that has no user-prefs of its own - can find it without prompting.
    /// Best-effort; never throws. Leaves the file untouched if it's a schema version we don't
    /// understand (there is intentionally no migration).
    /// </summary>
    void Remember(uint internalCheckSum, string internalRomGameName, string romPath);
}

[UsedImplicitly]
public class GlobalRomRegistry : IGlobalRomRegistry
{
    // Bump only on an incompatible on-disk schema change. There is deliberately NO migration: a file
    // whose version we don't recognize is ignored on read and left untouched on write, so different
    // Diz builds can't silently corrupt each other's registry.
    public const int ExpectedVersion = 1;

    private const string RootElementName = "DizGlobalRomRegistry";
    private const string EntryElementName = "Rom";
    private const string VersionAttrName = "Version";
    private const string CheckSumAttrName = "InternalCheckSum";
    private const string GameNameAttrName = "InternalRomGameName";
    private const string PathAttrName = "Path";

    public GlobalRomRegistry() : this(GetDefaultRegistryFilePath()) { }

    // path-injectable overload, mainly for tests
    public GlobalRomRegistry(string registryFilePath) => RegistryFilePath = registryFilePath;

    public string RegistryFilePath { get; }

    private static string GetDefaultRegistryFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), // %APPDATA% (Roaming) on Windows
            "DiztinGUIsh",
            "global-rom-registry.xml");

    public IEnumerable<string> FindCandidateRomPaths(uint internalCheckSum, string internalRomGameName)
    {
        var entries = TryLoadEntries();
        if (entries == null)
            return Array.Empty<string>();

        var wantName = internalRomGameName.Trim();

        // Only hand back paths whose STORED identity (checksum AND game name) already matches this
        // project. This is a cheap metadata compare - no ROM file is opened - and it keeps the ROM
        // search from opening and scanning whole files that we already know can't match. The paths
        // returned are still only hints: the caller re-opens and re-verifies each against the actual
        // ROM bytes. (Header titles are space-padded, so compare game names trimmed.) Paths are
        // normalized on the way out so equivalent spellings dedup and a lookup doesn't depend on the
        // process's current directory; a path that won't normalize is handed back as stored.
        return entries
            .Where(e => e.CheckSum == internalCheckSum
                        && string.Equals(e.GameName.Trim(), wantName, StringComparison.Ordinal)
                        && !string.IsNullOrWhiteSpace(e.Path))
            .Select(e => TryNormalizePath(e.Path, out var full) ? full : e.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Remember(uint internalCheckSum, string internalRomGameName, string romPath)
    {
        if (string.IsNullOrWhiteSpace(romPath))
            return;

        // Store a normalized absolute path so a machine-global registry doesn't depend on the process
        // cwd and equivalent spellings collapse to one entry. A path that won't normalize (invalid
        // chars, etc.) can't be stored meaningfully - skip it rather than crash the caller.
        if (!TryNormalizePath(romPath, out var normalizedRomPath))
            return;

        try
        {
            XDocument doc;
            XElement root;

            if (File.Exists(RegistryFilePath))
            {
                XDocument loaded;
                try
                {
                    loaded = XDocument.Load(RegistryFilePath);
                }
                catch (System.Xml.XmlException)
                {
                    // The file is malformed/truncated - e.g. a previous run was force-killed mid-write.
                    // Don't abort: fall through to build a fresh document and rewrite, so a corrupt
                    // registry self-heals instead of staying dead until a human deletes it.
                    loaded = null!;
                }

                if (loaded != null)
                {
                    var loadedRoot = loaded.Root;
                    if (loadedRoot == null || loadedRoot.Name.LocalName != RootElementName ||
                        ReadVersion(loadedRoot) != ExpectedVersion)
                        return; // well-formed but not a schema version we own: leave it untouched (no migration)
                    doc = loaded;
                    root = loadedRoot;
                }
                else
                {
                    root = new XElement(RootElementName, new XAttribute(VersionAttrName, ExpectedVersion));
                    doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
                }
            }
            else
            {
                root = new XElement(RootElementName, new XAttribute(VersionAttrName, ExpectedVersion));
                doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            }

            var wantName = internalRomGameName.Trim();
            var existing = root.Elements(EntryElementName).FirstOrDefault(e =>
                TryParseCheckSum(e, out var cs) && cs == internalCheckSum &&
                string.Equals(((string?)e.Attribute(GameNameAttrName) ?? "").Trim(), wantName, StringComparison.Ordinal));

            if (existing != null)
            {
                existing.SetAttributeValue(PathAttrName, normalizedRomPath);
            }
            else
            {
                root.Add(new XElement(EntryElementName,
                    new XAttribute(CheckSumAttrName, internalCheckSum.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(GameNameAttrName, internalRomGameName ?? ""),
                    new XAttribute(PathAttrName, normalizedRomPath)));
            }

            // CreateDirectory("") throws ArgumentException, so only create when there's a dir portion
            // (a bare filename has none - the cwd already exists).
            var dir = Path.GetDirectoryName(RegistryFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Write to a sibling temp file then atomically move it into place. A crash/kill mid-write
            // truncates the temp file, never the live registry, so the on-disk file is always either
            // the old complete version or the new complete version.
            var tempPath = RegistryFilePath + ".tmp";
            doc.Save(tempPath);
            File.Move(tempPath, RegistryFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            // Only ever a convenience cache; a failure here must never disrupt project loading.
            Console.WriteLine($"Warning: couldn't update the global ROM registry at '{RegistryFilePath}': {ex.Message}");
        }
    }

    private readonly record struct Entry(uint CheckSum, string GameName, string Path);

    // null => file missing/unreadable OR a schema version we don't understand (bail). empty list => no entries.
    private List<Entry>? TryLoadEntries()
    {
        try
        {
            if (!File.Exists(RegistryFilePath))
                return new List<Entry>();

            var doc = XDocument.Load(RegistryFilePath);
            var root = doc.Root;
            if (root == null || root.Name.LocalName != RootElementName)
                return null;

            if (ReadVersion(root) != ExpectedVersion)
            {
                Console.WriteLine($"Warning: ignoring global ROM registry '{RegistryFilePath}': unexpected schema version (want {ExpectedVersion}).");
                return null;
            }

            var list = new List<Entry>();
            foreach (var e in root.Elements(EntryElementName))
            {
                if (!TryParseCheckSum(e, out var cs))
                    continue;
                list.Add(new Entry(
                    cs,
                    (string?)e.Attribute(GameNameAttrName) ?? "",
                    (string?)e.Attribute(PathAttrName) ?? ""));
            }
            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: couldn't read the global ROM registry at '{RegistryFilePath}': {ex.Message}");
            return null;
        }
    }

    // Best-effort absolute-path normalization. Returns false (rather than throwing) for paths
    // Path.GetFullPath rejects - invalid characters, too long, etc. - so callers can just skip them.
    private static bool TryNormalizePath(string path, out string normalized)
    {
        try
        {
            normalized = Path.GetFullPath(path);
            return true;
        }
        catch (Exception)
        {
            normalized = "";
            return false;
        }
    }

    private static int ReadVersion(XElement root)
    {
        var v = (string?)root.Attribute(VersionAttrName);
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
    }

    private static bool TryParseCheckSum(XElement entry, out uint checkSum)
    {
        checkSum = 0;
        var raw = (string?)entry.Attribute(CheckSumAttrName);
        return raw != null && uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out checkSum);
    }
}
