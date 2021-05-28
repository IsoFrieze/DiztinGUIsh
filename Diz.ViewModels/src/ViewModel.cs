using ReactiveUI;

namespace Diz
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