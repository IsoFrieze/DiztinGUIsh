#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Diz.Core.serialization;

namespace Diz.Controllers.importers;

/// <summary>
/// One console's route from "a file on disk" to "the settings a new project is created from".
///
/// This is the platform axis: SNES today, and whatever else is taught to Diz later. It is
/// deliberately NOT the toolkit axis -- which window an importer puts on screen is a separate,
/// per-toolkit registration behind its own view seam, so adding a console costs one implementation
/// of this interface rather than one per toolkit.
///
/// Implementations own everything console-specific: how the file is read and validated, what the
/// user is asked, and what ends up in the settings. Nothing outside them may assume a particular
/// console.
/// </summary>
public interface IRomImporter
{
    /// <summary>
    /// Human-readable console name, e.g. "SNES". Shown to the user -- it is what the file picker's
    /// filter is labelled with.
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// File extensions this importer claims, lower-case and dot-prefixed, e.g. ".smc".
    /// Used to pick an importer for a file, and to build the file picker's filter.
    ///
    /// Claiming an extension is a hint, not a guarantee: a ROM with an unusual or missing extension
    /// still has to be importable, so nothing here is treated as a requirement.
    /// </summary>
    IReadOnlyList<string> FileExtensions { get; }

    /// <param name="romFilename">Path to the ROM file to import.</param>
    /// <returns>The settings to create a project from, or null if the user backed out.</returns>
    Task<ImportRomSettings?> ChooseImportSettingsAsync(string romFilename);
}
