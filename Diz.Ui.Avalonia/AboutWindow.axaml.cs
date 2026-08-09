using Avalonia.Controls;
using Avalonia.Interactivity;
using Diz.Core.Interfaces;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia "About Diz" window: it shows the running build's version and full description,
/// and takes an OK.
///
/// NO ViewModel, on purpose -- the window has no mutable state and no input of its own. The two
/// strings are read once, at construction, from the version source handed in; nothing about them
/// can change while the window is open.
///
/// Layout is in AboutWindow.axaml; the x:Name fields and InitializeComponent come from the
/// Avalonia XAML source generator -- never hand-write them, or the generator suppresses its own
/// version and every named control is null at runtime.
///
/// The window HIDES rather than closes, so the host can re-show this same instance instead of
/// stacking a second copy behind the first every time the menu item is picked.
/// </summary>
internal sealed partial class AboutWindow : Window
{
    /// <summary>Parameterless ctor required by the Avalonia XAML compiler (AVLN3000). Never
    /// used at runtime -- the window is always created with a version source. The version and
    /// description are left blank in that case; no ctor-body code dereferences the null, so
    /// tooling instantiation is still safe.</summary>
    public AboutWindow() : this(null!) { }

    public AboutWindow(IAppVersionInfo appVersionInfo)
    {
        InitializeComponent();

        // hide-on-close: the host keeps one window for the whole run and re-shows it.
        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };

        if (appVersionInfo == null)
            return;

        var version = appVersionInfo.GetVersionInfo(IAppVersionInfo.AppVersionInfoType.Version);
        var fullDescription =
            appVersionInfo.GetVersionInfo(IAppVersionInfo.AppVersionInfoType.FullDescription);

        Title = $"About Diz {version}";
        VersionText.Text = $"Version: {version}";
        DescriptionText.Text = fullDescription;
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e) => Close();
}
