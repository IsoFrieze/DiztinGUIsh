using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Diz.Controllers;
using Diz.Controllers.interfaces;
using Diz.Controllers.util;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.util;
using JetBrains.Annotations;
using Label = Diz.Core.model.Label;

namespace DiztinGUIsh.util
{
    /*[AttributeUsage(AttributeTargets.Property)]
    public class CellStyleFormatter : Attribute
    {
        public Func<Color?> BackgroundColorFormatter { get; }

        public CellStyleFormatter(Func<Color?> bgColorFormatter)
        {
            BackgroundColorFormatter = bgColorFormatter;
        }
    }*/

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class RomByteDataGridRow : RomByteRowBase, IDataGridRow
    {
        /// <summary>
        /// Format an arbitrary cell in the grid. it may or may not be the currently selected cell.
        /// </summary>
        /// <param name="colPropName">the name of the data property associated with this column (not the column header, this is the internal name)</param>
        /// <param name="style">Out param, modify this to set the style</param>
        public void SetStyleForCell(string colPropName, DataGridViewCellStyle style)
        {
            if (IsColumnEditable(colPropName))
                style.SelectionBackColor = Color.Chartreuse;

            // all cells in a row get this treatment
            switch (ByteEntry.TypeFlag)
            {
                case FlagType.Unreached:
                    style.BackColor = Color.LightGray;
                    style.ForeColor = Color.DarkSlateGray;
                    break;
                case FlagType.Opcode:
                    var color = GetBackgroundColorForMarkedAsOpcode(colPropName);
                    if (color != null)
                        style.BackColor = color.Value;
                    break;
                case FlagType.Operand:
                    style.ForeColor = Color.LightGray;
                    break;
                case FlagType.Graphics:
                    style.BackColor = Color.LightPink;
                    break;
                case FlagType.Music:
                    style.BackColor = Color.PowderBlue;
                    break;
                case FlagType.Data8Bit:
                case FlagType.Data16Bit:
                case FlagType.Data24Bit:
                case FlagType.Data32Bit:
                    style.BackColor = Color.NavajoWhite;
                    break;
                case FlagType.Pointer16Bit:
                case FlagType.Pointer24Bit:
                case FlagType.Pointer32Bit:
                    style.BackColor = Color.Orchid;
                    break;
                case FlagType.Text:
                    style.BackColor = Color.Aquamarine;
                    break;
                case FlagType.Empty:
                    style.BackColor = Color.DarkSlateGray;
                    style.ForeColor = Color.LightGray;
                    break;
            }

            SetStyleForIndirectAddress(colPropName, style);
        }

        private void SetStyleForIndirectAddress(string colPropName, DataGridViewCellStyle style)
        {
            var selectedRomByteRow = ParentView.SelectedByteOffset;
            if (selectedRomByteRow == null)
                return;

            var matchingIa = colPropName switch
            {
                nameof(Offset) => 
                    Data.IsMatchingIntermediateAddress(selectedRomByteRow.ParentIndex, ByteEntry.ParentIndex),
                nameof(IA) => 
                    Data.IsMatchingIntermediateAddress(ByteEntry.ParentIndex, selectedRomByteRow.ParentIndex),
                _ => false
            };

            if (matchingIa)
                style.BackColor = Color.DeepPink;
        }

        public ByteEntry Item
        {
            get => ByteEntry;
            init => ByteEntry = value;
        }
    }
    
    // TODO: consider moving all of this into some per-property attribute would be something like?
    // [CustomConfig(col =>
    // {
    //     col.DefaultCellStyle = new DataGridViewCellStyle
    //     {
    //         Alignment = DataGridViewContentAlignment.MiddleRight, Font = FontHuman,
    //     };
    //     col.MaxInputLength = 60;
    //     col.MinimumWidth = 6;
    //     col.Width = 200;
    // })]
    public static class RomByteDataGridRowFormatting {
        public static readonly Font FontData = new("Consolas", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        public static readonly Font FontHuman = new("Microsoft Sans Serif", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);

        private static readonly Dictionary<string, Action<DataGridViewTextBoxColumn>> CellProperties = new()
        {
            {
                nameof(RomByteDataGridRow.Label), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleRight, Font = FontHuman,
                    };
                    col.MaxInputLength = 60;
                    col.MinimumWidth = 6;
                    col.Width = 200;
                }
            },
            {
                nameof(RomByteDataGridRow.Offset), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleLeft, Font = FontData,
                    };
                    col.MaxInputLength = 6;
                    col.MinimumWidth = 6;
                    col.Width = 58;
                }
            },
            {
                nameof(RomByteDataGridRow.AsciiCharRep), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleRight, Font = FontData,
                    };
                    col.MaxInputLength = 1;
                    col.MinimumWidth = 6;
                    col.Width = 26;
                }
            },
            {
                nameof(RomByteDataGridRow.NumericRep), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleRight, Font = FontData,
                    };
                    col.MaxInputLength = 3;
                    col.MinimumWidth = 6;
                    col.Width = 26;
                }
            },
            {
                nameof(RomByteDataGridRow.Point), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter, Font = FontData,
                    };
                    col.MaxInputLength = 3;
                    col.MinimumWidth = 6;
                    col.Width = 34;
                }
            },
            {
                nameof(RomByteDataGridRow.Instruction), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleLeft, Font = FontData,
                    };
                    col.MaxInputLength = 64;
                    col.MinimumWidth = 6;
                    col.Width = 125;
                }
            },
            {
                nameof(RomByteDataGridRow.IA), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleLeft, Font = FontData,
                    };
                    col.MaxInputLength = 6;
                    col.MinimumWidth = 6;
                    col.Width = 58;
                }
            },
            {
                nameof(RomByteDataGridRow.TypeFlag), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleLeft, Font = FontData,
                    };
                    col.MinimumWidth = 6;
                    col.Width = 86;
                }
            },
            {
                nameof(RomByteDataGridRow.DataBank), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleRight, Font = FontData,
                    };
                    col.MaxInputLength = 2;
                    col.MinimumWidth = 6;
                    col.Width = 26;
                }
            },
            {
                nameof(RomByteDataGridRow.DirectPage), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleLeft, Font = FontData,
                    };
                    col.MaxInputLength = 4;
                    col.MinimumWidth = 6;
                    col.Width = 42;
                }
            },
            {
                nameof(RomByteDataGridRow.MFlag), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter, Font = FontData,
                    };
                    col.MaxInputLength = 2;
                    col.MinimumWidth = 6;
                    col.Width = 26;
                }
            },
            {
                nameof(RomByteDataGridRow.XFlag), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter, Font = FontData,
                    };
                    col.MaxInputLength = 2;
                    col.MinimumWidth = 6;
                    col.Width = 26;
                }
            },
            {
                nameof(RomByteDataGridRow.Comment), col =>
                {
                    col.DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleLeft, Font = FontHuman,
                        // WrapMode = DataGridViewTriState.False, // TODO: consider this?
                    };
                    col.MinimumWidth = 6;
                    // col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; // ULTRA EXPENSIVE, never use.
                }
            },
        };

        public static void ApplyFormatting(DataGridViewTextBoxColumn col) => CellProperties[col.DataPropertyName](col);
    }
}