using System.ComponentModel;
using Diz.Core.model;
using Diz.Core.util;
using ReactiveUI;

namespace Diz.Gui.ViewModels.ViewModels
{
    public class LabelViewModel : ViewModel
    {
        private LabelProxy labelProxy;

        public LabelProxy LabelProxy
        {
            get => labelProxy;
            set => this.RaiseAndSetIfChanged(ref labelProxy, value);
        }

        // no reason this can't have a "Set" on it.
        [DisplayName("Snes Address")]
        [ReadOnly(false)]
        public string SnesAddress => Util.ToHexString6(LabelProxy.Offset);
    }
}