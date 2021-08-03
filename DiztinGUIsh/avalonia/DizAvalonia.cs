using Avalonia;
using Avalonia.Logging;
using Avalonia.ReactiveUI;
using Diz.Gui.Avalonia.App;
using Diz.Gui.Avalonia.UserControls.UserControls;
using Diz.Gui.ViewModels.ViewModels;
using ReactiveUI;
using Splat;

namespace DiztinGUIsh
{
    public static class DizAvalonia
    {
        /// <summary>
        /// Embed Avalonia controls inside a winforms app.
        /// This thing is a little picky :)
        ///
        /// We're doing this to transition the app's UI slowly over to Avalonia, then, we'll ditch Winforms  
        /// </summary>
        public static void InitAvaloniaEmbeddedInWinforms()
        {
#if USE_AVALONIA_WINFORMS_EMBED
            // this references DizAvalonia.App, we might want to just move it into this app
            AppBuilder.Configure<App>()
                .LogToTrace(LogEventLevel.Verbose, LogArea.Binding, LogArea.Control, LogArea.Layout, LogArea.Property,
                    LogArea.Win32Platform, LogArea.Visual)
                //.UseWin32() // for winforms
                //.UseDirect2D1() // for WPF only (we're not using it)
                //.UseSkia() // rendering
                .UsePlatformDetect() // theoretically, this does everything above does
                .UseReactiveUI()
                .SetupWithoutStarting();

            Locator.CurrentMutable.Register(
                () => new LabelsListUserControl(), typeof(IViewFor<LabelViewModel>)
            );
            
            Locator.CurrentMutable.Register(
                () => new LabelsListUserControl(), typeof(IActivatableView)
            );
#endif
        }
    }
}