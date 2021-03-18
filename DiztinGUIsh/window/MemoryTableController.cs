using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.controller;
using Label = Diz.Core.model.Label;

namespace DiztinGUIsh.window
{
    /*public interface IMemoryTableController : IDataViewWithSelection
    {
        void KeyDown(object sender, KeyEventArgs e);
        
        void AddLabel(int offset, Label label, bool overwrite);
        void SetDataBank(int romOffset, int result);
        void SetDirectPage(int romOffset, int result);
        void SetMFlag(int romOffset, bool value);
        void SetXFlag(int romOffset, bool value);
        void AddComment(int i, string v, bool overwrite);
    }*/
    
    /*public class MemoryTableController : IMemoryTableController
    {
        public Data Data { get; init; }
        public MemoryTableUserControl TableControl { get; init; }

        public MainFormController MainFormController { get; init; } // not sure we should really have this. hack for now.

        public bool MoveWithStep => MainFormController.MoveWithStep;
        
        public void Invalidate()
        {
            // TableControl.InvalidateTable();
        }

        public void Init()
        {
            TableControl.Init();
        }

        public int GetSelectedOffset()
        {
            return SelectedSnesOffset;
            // return TableControl.GetSelectedOffset();
        }

        /*public void InvalidateTable() => TableControl.InvalidateTable();
        public void UpdateDataGridView() => TableControl.UpdateDataGridView();#1#
        private bool RomDataPresent() => Data?.GetRomSize() > 0;

        private void RefreshPercentAndWindowTitle()
        {
            // mainForm.RefreshPercentAndWindowTitle();
            throw new System.NotImplementedException();
        }

        public void SelectOffset(int destination)
        {
            SelectedSnesOffset = destination;
        }

        public void KeyDown(object sender, KeyEventArgs e)
        {
            var offset = GetSelectedOffset();
            
            switch (e.KeyCode)
            {
                // actions
                case Keys.S:
                    MainFormController.Step(offset);
                    break;
                case Keys.I:
                    MainFormController.StepIn(offset);
                    break;
                case Keys.A:
                    MainFormController.AutoStepSafe(offset);
                    break;
                case Keys.T:
                    MainFormController.GoToIntermediateAddress(offset);
                    break;
                case Keys.U:
                    MainFormController.GoToUnreached(true, true);
                    break;
                case Keys.H:
                    MainFormController.GoToUnreached(false, false);
                    break;
                case Keys.N:
                    MainFormController.GoToUnreached(false, true);
                    break;
                case Keys.K:
                    MainFormController.Mark(offset);
                    break;
                case Keys.M:
                    MainFormController.SetMFlag(offset, !Data.GetMFlag(offset));
                    break;
                case Keys.X:
                    MainFormController.SetXFlag(offset, !Data.GetXFlag(offset));
                    break;
            }
        }

        public void AddLabel(int offset, Label label, bool overwrite)
        {
            Data?.AddLabel(offset, label, overwrite);
        }

        public void SetDataBank(int romOffset, int result)
        {
            Data?.SetDataBank(romOffset, result);
        }

        public void SetDirectPage(int romOffset, int result)
        {
            Data?.SetDirectPage(romOffset, result);
        }

        public void SetMFlag(int romOffset, bool value)
        {
            Data?.SetMFlag(romOffset, value);
        }

        public void SetXFlag(int romOffset, bool value)
        {
            Data?.SetXFlag(romOffset, value);
        }

        public void AddComment(int i, string v, bool overwrite)
        {
            Data?.AddComment(i, v, overwrite);
        }

        public int SelectedSnesOffset { get; set; }
        public int StartingOffset { get; set; }
        
        // Data offset of the selected row. this is a ROM OFFSET (data), not row offset (view)
        public int Count
        {
            get => TableControl.RowsToShow;
            set => TableControl.RowsToShow = value;
        }

        public void BeginAddingLabel()
        {
            if (!RomDataPresent())
                return;
            
            // TableControl.BeginEditingLabel();
        }
        
        public void BeginEditingComment()
        {
            if (!RomDataPresent())
                return;
            
            // TableControl.BeginEditingComment();
        }
    }
    */
    
    // location inside a ROM (i.e. offset into the file)
    /*public class Location
    {
        protected int _value;

        protected Location(int value)
        {
            _value = value;
        }

        public static implicit operator int(Location value)
        {
            return value._value;
        }
    }

    // memory address in a SNES ROM
    public class LocationRom : Location
    {
        public LocationRom(int value) : base(value) { }
        
        public static implicit operator LocationRom(int value)
        {
            return new(value);
        }
    }
    
    // memory address in the SNES S-CPU memory bus
    public class LocationAddress : Location
    {
        public LocationAddress(int value) : base(value) { }
        
        public static implicit operator LocationAddress(int value)
        {
            return new(value);
        }
    }*/
}