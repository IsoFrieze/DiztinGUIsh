using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Diz.Controllers.interfaces;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia implementation of the step-4 file-dialog seam, built on IStorageProvider --
/// the async-only file API whose existence is the reason IFileDialogService is async in the
/// first place (see the interface docs in Diz.Controllers).
///
/// IStorageProvider is obtained from a live TopLevel, so a host window must register itself
/// via <see cref="DialogOwner"/> before dialogs can be shown. With no owner (no Avalonia
/// window open yet) every prompt returns null, which callers already treat as "cancelled".
/// </summary>
public class AvaloniaFileDialogService : IFileDialogService
{
    /// <summary>The window dialogs are parented to. Set by the Avalonia view host when its
    /// window is created; null means "no Avalonia window open" and prompts return null.</summary>
    public TopLevel? DialogOwner { get; set; }

    public async Task<string?> PromptOpenFileAsync(string title, string filter)
    {
        var storageProvider = DialogOwner?.StorageProvider;
        if (storageProvider == null)
            return null;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = string.IsNullOrEmpty(title) ? null : title,
            AllowMultiple = false,
            FileTypeFilter = ParseWinformsFilter(filter),
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PromptSaveFileAsync(string title, string filter, string? suggestedName = null)
    {
        var storageProvider = DialogOwner?.StorageProvider;
        if (storageProvider == null)
            return null;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = string.IsNullOrEmpty(title) ? null : title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = ParseWinformsFilter(filter),
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PromptSelectFolderAsync(string title, string? initialPath = null)
    {
        var storageProvider = DialogOwner?.StorageProvider;
        if (storageProvider == null)
            return null;

        IStorageFolder? startLocation = null;
        if (!string.IsNullOrEmpty(initialPath))
            startLocation = await storageProvider.TryGetFolderFromPathAsync(initialPath);

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = string.IsNullOrEmpty(title) ? null : title,
            AllowMultiple = false,
            SuggestedStartLocation = startLocation,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    /// <summary>
    /// The seam's filter strings are WinForms syntax ("Name|*.ext|Name2|*.ext2;*.ext3") by
    /// contract; translate to Avalonia's FilePickerFileType list. Malformed trailing parts
    /// (odd count) are ignored rather than thrown on -- a filter bug should never make a
    /// file dialog unusable.
    /// </summary>
    public static List<FilePickerFileType> ParseWinformsFilter(string? filter)
    {
        var result = new List<FilePickerFileType>();
        if (string.IsNullOrWhiteSpace(filter))
            return result;

        var parts = filter.Split('|');
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            var patterns = parts[i + 1]
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (patterns.Count == 0)
                continue;

            result.Add(new FilePickerFileType(parts[i].Trim()) { Patterns = patterns });
        }

        return result;
    }
}
