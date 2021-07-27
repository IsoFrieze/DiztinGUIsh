using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Diz.Gui.Avalonia.Views.Windows;

namespace Diz.Gui.Avalonia
{
    internal static class Program
    {
        private static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder
                .Configure<App.App>()
                .UseReactiveUI()
                .UsePlatformDetect()
                .LogToTrace();
        }

        private static void AppMain(Application app, string[] args)
        {
            new MainWindow().Show(); // tmp
            app.Run(new MainWindow());
        }

        public static void Main(string[] args)
        {
            BuildAvaloniaApp().Start(AppMain, args);
        }
    }
}