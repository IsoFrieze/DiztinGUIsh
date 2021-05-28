using Avalonia;
using Avalonia.Markup.Xaml;

namespace Diz.Gui.Avalonia.App {
    public class App : Application {
        public override void Initialize() {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
