using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Be.Windows.Forms;
using Diz.Core.model;
using DiztinGUIsh.util;
using Equin.ApplicationFramework;

namespace DiztinGUIsh.window2
{
    public partial class DataGridEditorTest : Form
    {
        public DataGridEditorTest()
        {
            InitializeComponent();
        }

        private void DG_Load(object sender, System.EventArgs e)
        {
            
        }

        // was ArraySegment<RomByte>
        // this is looking good.
        public void LoadData(RomBytes dataSubset)
        {
            // rando stuff
            dataGridView1.AutoGenerateColumns = true;
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
            dataGridView1.BorderStyle = BorderStyle.Fixed3D;
            dataGridView1.EditMode = DataGridViewEditMode.EditOnEnter;
            dataGridView1.VirtualMode = true;

            // real stuff
            // dataGridView1.DataSource = dataSubset.Array; // THIS WORKED
            // dataGridView1.DataSource = (IList<RomByte>)dataSubset;
            // dataGridView1.DataSource = dataSubset.ToArray(); // FULL ITEM LIST, NOT SUBSET // THIS WORKS
            // dataGridView1.DataSource = dataSubset.ToArray(); // WORKS
            
            // dumb test. this should be the ASCII title of the game in the ROM header
            //var startingOffset = 0xFFC0;
            //var count = 0x15;
            var startingOffset = 15;
            var count = 10;

            // works, but... dont need it anym,ore
            // var slice = new ArraySegment<RomByte>(dataSubset.ToArray(), startingOffset, count);
            // nah didn't work var dataview = new BindingList<RomBytes>(slice);

            var view = new BindingListView<RomByteData>(dataSubset);
            view.Filter = new PredicateItemFilter<RomByteData>((item) => item.TypeFlag == FlagType.Opcode);
            dataGridView1.DataSource = view;
        }
    }
}