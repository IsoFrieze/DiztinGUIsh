using ReactiveUI;

namespace Diz.ViewModels
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