using System.Drawing;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;

namespace DiztinGUIsh.window.usercontrols
{
    public partial class BankLegend : UserControl
    {
        public BankLegend()
        {
            InitializeComponent();
        }

        private void AddControl(string name, Color color)
        {
            flowLayoutPanel1.Controls.Add(
                new BankLegendItem(name, color)
            );
        }

        private void BankLegend_Load(object sender, System.EventArgs e)
        {
            var enums = Util.GetEnumColorDescriptions<Data.FlagType>();
            foreach (var en in enums)
            {
                AddControl(en.Key.ToString(), Util.GetColorFromFlag(en.Key));
            }
        }
    }
}
