using System.Drawing;
using System.Windows.Forms;

namespace DiztinGUIsh.window.usercontrols
{
    public partial class BankLegendItem : UserControl
    {
        public BankLegendItem(string labelText, Color color)
        {
            InitializeComponent();

            label1.Text = labelText;
            pictureBox1.BackColor = color;
        }
    }
}
