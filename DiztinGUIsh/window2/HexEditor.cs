using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Windows.Forms;
using Be.Windows.Forms;
using Diz.Core.model;

namespace DiztinGUIsh.window2
{
    public interface IGarbageWhateverForm
    {
        public void LoadData(ArraySegment<RomByte> dataSubset);
    }
    
    public partial class HexEditor : Form, IGarbageWhateverForm
    {
        public HexEditor()
        {
            InitializeComponent();

            hexBox1.StringViewVisible = true;
            hexBox1.ReadOnly = true;
        }

        private void HexEditor_Load(object sender, System.EventArgs e)
        {
            
        }

        public void LoadData(ArraySegment<RomByte> dataSubset)
        {
            var bs = new BindingSource(dataSubset.Array, "");


            // var bytes = dataSubset.GetBytes().ToList();
            // hexBox1.ByteProvider = new DynamicByteProvider(dataSubset.ToImmutableArray());
        }
    }
}