#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace Diz.Controllers.importers;

/// <summary>
/// The set of consoles Diz can import, and the choice of which one a given file belongs to.
///
/// Everything an importer needs to be reachable is on <see cref="IRomImporter"/>, so a new console
/// is added by registering one more implementation with the container -- the file picker widens and
/// detection starts routing to it with no change here or at any call site.
/// </summary>
[UsedImplicitly]
public class RomImporterRegistry
{
    /// <summary>Shown for the "any file" entry in the file picker, matching the platform entries.</summary>
    public const string AllFilesFilterEntry = "All files|*.*";

    private readonly IReadOnlyList<IRomImporter> importers;

    public RomImporterRegistry(IEnumerable<IRomImporter> importers)
    {
        this.importers = importers.ToList();
    }

    /// <summary>
    /// Every registered importer. The order is whatever the container hands over -- it is NOT the
    /// order they were registered in -- so it decides only cosmetic things: which console is listed
    /// first in the file picker, and which one would win if two ever claimed the same extension.
    /// </summary>
    public IReadOnlyList<IRomImporter> Importers => importers;

    /// <summary>
    /// Work out which console <paramref name="romFilename"/> is for. Matching is on the file
    /// extension, lower-cased.
    ///
    /// A file whose extension no importer claims still gets imported when there is only one
    /// importer to choose from. That is not a fallback for tidiness' sake: ROMs are routinely
    /// renamed to .bin, .rom, or nothing at all, and refusing those would reject files that import
    /// perfectly well. With one importer there is no ambiguity to resolve, so the file goes to it.
    ///
    /// Once a second console is registered that reasoning stops holding -- an unrecognised
    /// extension no longer identifies anything, and guessing would silently produce a project
    /// analysed as the wrong console. So this returns null instead, and the caller says it could not
    /// tell. That is the point at which the user needs a way to say which console it is; until
    /// then the case is unreachable in the app and exists only to keep the guess from being made.
    /// </summary>
    /// <returns>The importer to use, or null if it cannot be determined.</returns>
    public IRomImporter? SelectFor(string romFilename)
    {
        var extension = GetExtensionLowercase(romFilename);

        var matched = importers.FirstOrDefault(
            importer => importer.FileExtensions.Contains(extension));

        if (matched != null)
            return matched;

        return importers.Count == 1 ? importers[0] : null;
    }

    private static string GetExtensionLowercase(string romFilename) =>
        string.IsNullOrEmpty(romFilename)
            ? ""
            : Path.GetExtension(romFilename).ToLowerInvariant();

    /// <summary>
    /// The file picker's filter, built from what is registered: one entry per console listing its
    /// extensions, then an "all files" entry -- which has to stay, because a ROM with an unusual or
    /// missing extension is still importable and the user must be able to reach it.
    /// </summary>
    public string BuildFileDialogFilter()
    {
        var entries = importers
            .Select(importer =>
                $"{importer.PlatformName} ROM Images|" +
                string.Join(";", importer.FileExtensions.Select(extension => $"*{extension}")))
            .Append(AllFilesFilterEntry);

        return string.Join("|", entries);
    }
}
