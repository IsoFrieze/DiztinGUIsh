using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;

namespace Diz.Gui.Avalonia
{
    // You may want to start here:
    // https://reactiveui.net/docs/getting-started/

    internal static class Program
    {
        // http://avaloniaui.net/docs/reactiveui/
        // https://github.com/AvaloniaUI/Avalonia/wiki/Application-lifetimes
        private static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder
                .Configure<App>()
                .UseReactiveUI()
                .UsePlatformDetect()
                .LogToTrace();
        }

        private static void AppMain(Application app, string[] args)
        {
            app.Run(new MainView());
        }

        public static void Main(string[] args)
        {
            BuildAvaloniaApp().Start(AppMain, args);
        }
    }
}