using ReactiveUI;

namespace Diz.Gui.Avalonia
{
    public class ViewModel : ReactiveObject, IActivatableViewModel
    {
        public ViewModelActivator Activator { get; }

        public ViewModel()
        {
            Activator = new ViewModelActivator();
        }
    }
}