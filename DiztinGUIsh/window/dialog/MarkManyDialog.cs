using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using Diz.Controllers.interfaces;
using Diz.Core;
using Diz.Core.commands;
using Diz.Core.model;
using Diz.Core.util;

// be careful modifying anything in this class, it's extremely fragile and hardcoded.

namespace DiztinGUIsh.window.dialog
{
    public partial class MarkManyView : Form, IMarkManyView
    {
        public IMarkManyController Controller { get; set; }
        private IReadOnlySnesRomBase Data => Controller.Data;
        
        private int PropertyMaxIntVal => Property == MarkCommand.MarkManyProperty.DataBank 
            ? 0x100 
            : 0x10000;

        public MarkCommand.MarkManyProperty Property
        {
            get => (MarkCommand.MarkManyProperty) comboPropertyType.SelectedIndex;
            set
            {
                UpdatePropertyIndex(value);
                UpdateVisibility();
                UpdateTextUi();
            }
        }
        
        private Util.NumberBase NoBase => 
            radioDec.Checked ? Util.NumberBase.Decimal : Util.NumberBase.Hexadecimal;
        private int DigitCount => NoBase == Util.NumberBase.Hexadecimal && radioSNES.Checked ? 6 : 0;
        
        private int PropertyValueAsInt => 
            comboPropertyType.SelectedIndex == 1 ? 
                Data.GetDataBank(Controller.DataRange.StartIndex) : 
                Data.GetDirectPage(Controller.DataRange.StartIndex);
        
        private int propertyValueIntDpOrD;
        private bool isUpdatingText;

        /// <summary>
        /// Dialog that lets us mark many of a particular column on the data grid form
        /// </summary>
        /// <param name="column">Which column we're marking many of (determines UI elements)</param>
        /// <param name="data">Rom we would be marking the data against</param>
        public MarkManyView()
        {
            InitializeComponent();
            InitCombos();
        }

        private void InitCombos()
        {
            flagCombo.SelectedIndex = 3;
            archCombo.SelectedIndex = 0;
            mxCombo.SelectedIndex = 0;
        }
        
        private void UpdatePropertyIndex(MarkCommand.MarkManyProperty eProperty)
        {
            // TODO: woof. fixme :) very, very hardcoded
            comboPropertyType.SelectedIndex = (int) eProperty;
        }
        
        private void ClampPropertyValue() => 
            propertyValueIntDpOrD = Util.ClampIndex(propertyValueIntDpOrD, PropertyMaxIntVal);

        public FlagType GetFlagTypeFromComboBox() =>
            flagCombo.SelectedIndex switch
            {
                0 => FlagType.Unreached,
                1 => FlagType.Opcode,
                2 => FlagType.Operand,
                3 => FlagType.Data8Bit,
                4 => FlagType.Graphics,
                5 => FlagType.Music,
                6 => FlagType.Empty,
                7 => FlagType.Data16Bit,
                8 => FlagType.Pointer16Bit,
                9 => FlagType.Data24Bit,
                10 => FlagType.Pointer24Bit,
                11 => FlagType.Data32Bit,
                12 => FlagType.Pointer32Bit,
                13 => FlagType.Text,
                _ => 0
            };
        
        public int GetComboxBoxIndexFromFlagType(FlagType flagType) =>
            flagType switch
            {
                FlagType.Unreached => 0,
                FlagType.Opcode => 1,
                FlagType.Operand => 2,
                FlagType.Data8Bit => 3,
                FlagType.Graphics => 4,
                FlagType.Music => 5,
                FlagType.Empty => 6,
                FlagType.Data16Bit => 7,
                FlagType.Pointer16Bit => 8,
                FlagType.Data24Bit => 9,
                FlagType.Pointer24Bit => 10,
                FlagType.Data32Bit => 11,
                FlagType.Pointer32Bit => 12,
                FlagType.Text => 13,
                _ => 0
            };

        public Architecture GetCpuArchFromComboBox() =>
            archCombo.SelectedIndex switch
            {
                0 => Architecture.Cpu65C816,
                1 => Architecture.Apuspc700,
                2 => Architecture.GpuSuperFx,
                _ => 0
            };
        
        public int GetComboBoxFromCpuArch(Architecture arch) =>
            arch switch
            {
                Architecture.Cpu65C816 => 0,
                Architecture.Apuspc700 => 1,
                Architecture.GpuSuperFx => 2,
                _ => 0
            };
        
        private int GetPropertyValueRaw() => 
            propertyValueIntDpOrD;

        private bool GetMorXFromComboBox() => mxCombo.SelectedIndex != 0;
        private int GetComboBoxFromMorX(bool flag) => flag ? 1 : 0;

        // this.... sucks. woof. need to rewrite
        public object GetPropertyValue() => 
            GetPropertyValue(comboPropertyType.SelectedIndex);

        private object GetPropertyValue(int whichProperty)
        {
            // ReSharper disable once HeapView.BoxingAllocation
            return whichProperty switch
            {
                0 => GetFlagTypeFromComboBox(),
                1 => GetPropertyValueRaw(),
                2 => GetPropertyValueRaw(),
                3 => GetMorXFromComboBox(),
                4 => GetMorXFromComboBox(),
                5 => GetCpuArchFromComboBox(),
                _ => 0
            };
        }

        public void AttemptSetSettings(MarkCommand.MarkManyProperty markProperty, object markValue)
        {
            if (markValue == null)
                return;

            // wooooooofffffffff..... fixme. jank as hell.
            try
            {
                switch (markProperty)
                {
                    case MarkCommand.MarkManyProperty.Flag:
                        flagCombo.SelectedIndex = GetComboxBoxIndexFromFlagType((FlagType) markValue);
                        break;
                    case MarkCommand.MarkManyProperty.DataBank:
                    case MarkCommand.MarkManyProperty.DirectPage:
                        propertyValueIntDpOrD = (int) markValue;
                        break;
                    case MarkCommand.MarkManyProperty.MFlag:
                    case MarkCommand.MarkManyProperty.XFlag:
                        mxCombo.SelectedIndex = GetComboBoxFromMorX((bool) markValue);
                        break;
                    case MarkCommand.MarkManyProperty.CpuArch:
                        archCombo.SelectedIndex = GetComboBoxFromCpuArch((Architecture) markValue);
                        break;
                }
            }
            catch (Exception)
            {
                // NOP
            }
        }

        public void AttemptSetSettings(Dictionary<MarkCommand.MarkManyProperty, object> settings)
        {
            // TODO: this doesn't work yet for the properties that are shared like D and DP.
            // we need to make the UI read from these settings instead of stuffing their values into them
            // one-time. For now, it's still a decent way to go.
            
            foreach (var kvp in settings)
            {
                var settingsProperty = kvp.Key;
                var settingsValue = kvp.Value;

                AttemptSetSettings(settingsProperty, settingsValue);
            }
        }

        public Dictionary<MarkCommand.MarkManyProperty, object> SaveCurrentSettings()
        {
            var outputSettings = new Dictionary<MarkCommand.MarkManyProperty, object>();
            
            for (var i = 0; i < comboPropertyType.Items.Count; ++i)
            {
                var val = GetPropertyValue(i);
                outputSettings.Add((MarkCommand.MarkManyProperty) i, val);
            }

            return outputSettings;
        }

        public bool PromptDialog() => ShowDialog() == DialogResult.OK;

        private void UpdateVisibility()
        {
            var property = Property;
            
            flagCombo.Visible = 
                property == MarkCommand.MarkManyProperty.Flag;
            
            regValue.Visible = 
                property == MarkCommand.MarkManyProperty.DataBank || 
                property == MarkCommand.MarkManyProperty.DirectPage;
            
            mxCombo.Visible = 
                property == MarkCommand.MarkManyProperty.MFlag || 
                property == MarkCommand.MarkManyProperty.XFlag;
            
            archCombo.Visible = 
                property == MarkCommand.MarkManyProperty.CpuArch;

            regValue.MaxLength = 
                property == MarkCommand.MarkManyProperty.DataBank ? 3 : 5;
            
            propertyValueIntDpOrD = PropertyValueAsInt;
        }

        private void UpdateTextUi(TextBox selected = null)
        {
            ClampPropertyValue();
            
            isUpdatingText = true;
            if (selected != textStart) UpdateStartText();
            if (selected != textEnd) UpdateEndText();
            if (selected != textCount) UpdateCountText();
            if (selected != regValue) UpdateRegValueText();
            isUpdatingText = false;
        }

        private void UpdateRegValueText() => 
            regValue.Text = Util.NumberToBaseString(propertyValueIntDpOrD, NoBase, 0);

        private void UpdateCountText() => 
            textCount.Text = Util.NumberToBaseString(Controller.DataRange.RangeCount, NoBase, 0);

        private void UpdateEndText() => 
            textEnd.Text = Util.NumberToBaseString(radioSNES.Checked ? Data.ConvertPCtoSnes(Controller.DataRange.EndIndex) : Controller.DataRange.EndIndex, NoBase, DigitCount);

        private void UpdateStartText() =>
            textStart.Text =
                Util.NumberToBaseString(radioSNES.Checked ? Data.ConvertPCtoSnes(Controller.DataRange.StartIndex) : Controller.DataRange.StartIndex, NoBase, DigitCount);

        private void property_SelectedIndexChanged(object sender, EventArgs e) => UpdateVisibility();

        private bool IsSNESAddress => radioSNES.Checked;
        private int ConvertToRomPcOffsetIfNeeded(int v) => IsSNESAddress ? Data.ConvertSnesToPc(v) : v;
        
        private void regValue_TextChanged(object sender, EventArgs e)
        {
            var style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;

            if (!int.TryParse(regValue.Text, style, null, out var result)) 
                return;
            
            propertyValueIntDpOrD = result;
        }
        
        private void OnTextChanged(TextBox textBox, Action<int> onResult)
        {        
            if (isUpdatingText)
                return;

            isUpdatingText = true;
            var style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;

            if (int.TryParse(textBox.Text, style, null, out var result))
                onResult(result);
            
            UpdateTextUi(textBox);
        }
        
        
        private void radioHex_CheckedChanged(object sender, EventArgs e) => UpdateTextUi();
        private void radioROM_CheckedChanged(object sender, EventArgs e) => UpdateTextUi();

        private void okay_Click(object sender, EventArgs e) => DialogResult = DialogResult.OK;
        private void cancel_Click(object sender, EventArgs e) => Close();

        
        #region Range Actual Updates

        private void textCount_TextChanged(object sender, EventArgs e) => 
            OnTextChanged(textCount, newCount =>
            {
                SetRangeValuesManually(Controller.DataRange, 
                    -1, -1, newCount);
            });

        private void textEnd_TextChanged(object sender, EventArgs e) =>
            OnTextChanged(textEnd, newEndIndex =>
            {
                SetRangeValuesManually(Controller.DataRange, 
                    -1, ConvertToRomPcOffsetIfNeeded(newEndIndex), -1);
            });

        private void textStart_TextChanged(object sender, EventArgs e) => 
            OnTextChanged(textStart, newStartIndex =>
            {
                SetRangeValuesManually(Controller.DataRange, 
                    ConvertToRomPcOffsetIfNeeded(newStartIndex), -1, -1);
            });

        public static void SetRangeValuesManually(IDataRange dataRange, int newStartIndex = -1, int newEndIndex = -1, int newCount = -1)
        {
            if (dataRange == null)
                return;
            
            // info: "DataRange" has a start, end, and a count. if you change one,
            // the other 2 reflect that change. neat. however, the implementation ends up
            // not being the most UX friendly so, we're going to control it more directly here.
            //
            // let's do everything in terms of the StartIndex

            var oldEndIndex = dataRange.EndIndex;
            var oldStartIndex = dataRange.StartIndex;

            if (newStartIndex != -1)
            {
                Debug.Assert(newEndIndex == -1 && newCount == -1);

                // if changing START.  leave END, change # of bytes.
                var updatedCount = oldEndIndex - newStartIndex + 1;
                if (updatedCount < 0)
                    updatedCount = 1;

                dataRange.ManualUpdate(newStartIndex, updatedCount);
            } 
            else if (newEndIndex != -1)
            {
                //if changing END, leave START, change # of bytes.
                Debug.Assert(newCount == -1);
                
                var updatedCount = newEndIndex - oldStartIndex + 1;
                if (updatedCount < 0)
                    updatedCount = 1;
                
                dataRange.ManualUpdate(oldStartIndex, updatedCount);
            }
            else if (newCount != -1)
            {
                // if changing # bytes, leave START, change END
                // var updatedEndIndex = oldStartIndex + (newCount - 1);

                dataRange.ManualUpdate(oldStartIndex, newCount);
            }
        }
        #endregion
    }
}
