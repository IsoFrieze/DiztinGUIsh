using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Diz.Core.model;
using Diz.Gui.Avalonia.Views.Windows;
using Diz.Gui.ViewModels.ViewModels;

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
            // use the sample Diz project for this demo
            
            // use sample data
            var sampleData = SampleDataService.SourceData.Value;

            var mainWindow1 = CreateMainWindow(sampleData);
            mainWindow1.Show();

            var mainWindow2 = CreateMainWindow(sampleData);
            app.Run(mainWindow2);
        }

        private static MainWindow CreateMainWindow(Data sampleData)
        {
            var mainWindow = new MainWindow(sampleData);

            return mainWindow;
        }

        public static void Main(string[] args)
        {
            BuildAvaloniaApp().Start(AppMain, args);
        }
    }
}