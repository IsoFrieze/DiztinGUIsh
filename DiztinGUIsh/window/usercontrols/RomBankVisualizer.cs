using System.Windows.Forms;
using Diz.Core.model;

// this usercontrol shows:
// 1) a visual of one bank of ROM data
// 2) other GUI elements (like a label for the bank name)
//
// If you create a bunch of these you can view the entire ROM as a series of banks

namespace DiztinGUIsh.window.usercontrols
{
    public partial class RomBankVisualizer : UserControl
    {
        public RomBankVisualizer(Project project, int startingRomOffset, int length, string bankName)
        {
            InitializeComponent();

            romImage1.Project = project;
            romImage1.ROMVisual.RomStartingOffset = startingRomOffset;
            romImage1.ROMVisual.LengthOverride = length;
            lblBankName.Text = bankName;
        }
    }
}
