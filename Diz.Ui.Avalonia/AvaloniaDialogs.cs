using Avalonia.Controls;
using Avalonia.Layout;

namespace Diz.Ui.Avalonia;

/// <summary>
/// Minimal code-only message/confirm dialogs for the Avalonia backend. Deliberately plain:
/// they replace WinForms MessageBox call sites one-for-one so an Avalonia-hosted editor
/// never pops a WinForms window (new-ui plan decision 4 -- no mixed toolkits, and a
/// message box is a window).
/// </summary>
internal static class AvaloniaDialogs
{
    /// <summary>OK-only message dialog (replaces MessageBox.Show(msg, title, OK)).</summary>
    public static Task ShowMessageAsync(Window owner, string title, string message) =>
        ShowCore(owner, title, message, okText: "OK", cancelText: null);

    /// <summary>OK/Cancel confirm (replaces MessageBox.Show(msg, "Warning", OKCancel)).
    /// Returns true only if the user pressed OK.</summary>
    public static async Task<bool> ConfirmAsync(Window owner, string title, string message) =>
        await ShowCore(owner, title, message, okText: "OK", cancelText: "Cancel");

    private static async Task<bool> ShowCore(
        Window owner, string title, string message, string okText, string? cancelText)
    {
        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            MaxWidth = 560,
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        var okButton = new Button { Content = okText, MinWidth = 80, IsDefault = true };
        okButton.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(okButton);

        if (cancelText != null)
        {
            var cancelButton = new Button { Content = cancelText, MinWidth = 80, IsCancel = true };
            cancelButton.Click += (_, _) => dialog.Close(false);
            buttons.Children.Add(cancelButton);
        }

        dialog.Content = new StackPanel
        {
            Margin = new global::Avalonia.Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                },
                buttons,
            },
        };

        return await dialog.ShowDialog<bool?>(owner) == true;
    }
}
