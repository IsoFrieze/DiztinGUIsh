using Avalonia;
using Avalonia.Markup.Xaml;

namespace Diz.Gui.Avalonia {
    public class App : Application {
        public override void Initialize() {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
