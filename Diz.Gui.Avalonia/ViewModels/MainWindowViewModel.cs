using Diz.Core.model;
using Diz.Gui.ViewModels;
using Diz.Gui.ViewModels.ViewModels;

namespace Diz.Gui.Avalonia.ViewModels
{
    public class MainWindowViewModel : ViewModel
    {
        public MainWindowViewModel(Data data)
        {
            LabelsViewModel = new LabelsViewModel
            {
                SourceLabels = data?.ConnectLabels()
            };
        }

        public LabelsViewModel LabelsViewModel { get; }
    }
}