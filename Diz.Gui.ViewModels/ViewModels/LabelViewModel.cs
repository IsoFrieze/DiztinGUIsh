using System.ComponentModel;
using Diz.Core.model;
using Diz.Core.util;
using ReactiveUI;

namespace Diz.Gui.ViewModels.ViewModels
{
    public class LabelViewModel : ViewModel
    {
        private Label label;

        public Label Label
        {
            get => label;
            set => this.RaiseAndSetIfChanged(ref label, value);
        }

        public int Offset { get; set; }
        
        [DisplayName("Snes Address")]
        [ReadOnly(false)]
        public string SnesAddress => Util.ToHexString6(Offset);  
    }
}