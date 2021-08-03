using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Diz.Core;
using Diz.Core.interfaces;
using Diz.Core.model;
using Diz.Gui.Avalonia.UserControls.UserControls;
using DiztinGUIsh.Properties;

namespace DiztinGUIsh.window
{
    public partial class LabelsListWinForm : Form, IViewLabels, INotifyPropertyChangedExt
    {
        private Project project;
        private LabelsListUserControl LabelsListUserControl { get; set; }
        
        public LabelsListWinForm()
        {
            InitializeComponent();
            
            LabelsListUserControl = new LabelsListUserControl();
            avaloniaHost.Content = LabelsListUserControl;
            
            // NECESSARY? UGH. https://github.com/AvaloniaUI/Avalonia/issues/4951
            ((TopLevel)avaloniaHost.Content.GetVisualRoot()).Renderer.Start();
        }

        public Project Project
        {
            get => project;
            set
            {
                if (!this.SetField(ref project, value))
                    return;
                
                if (LabelsListUserControl.ViewModel != null)
                    LabelsListUserControl.ViewModel.SourceLabels = project?.Data?.ConnectLabels();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        public void OnPropertyChanged([CallerMemberName] string propertyName = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}