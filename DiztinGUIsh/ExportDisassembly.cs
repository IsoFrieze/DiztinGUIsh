using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiztinGUIsh
{
    public partial class ExportDisassembly : Form
    {
        public ExportDisassembly()
        {
            InitializeComponent();
            numData.Value = LogCreator.dataPerLine;
            textFormat.Text = LogCreator.format;
            comboUnlabeled.SelectedIndex = (int)LogCreator.unlabeled;
            comboStructure.SelectedIndex = (int)LogCreator.structure;
            UpdateSample();
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }

        private void textFormat_TextChanged(object sender, EventArgs e)
        {
            if (ValidateFormat())
            {
                LogCreator.format = textFormat.Text.ToLower();
                UpdateSample();
                button2.Enabled = true;
            } else
            {
                textSample.Text = "Invalid format!";
                button2.Enabled = false;
            }
        }

        private void numData_ValueChanged(object sender, EventArgs e)
        {
            LogCreator.dataPerLine = (int)numData.Value;
            UpdateSample();
        }

        private void comboUnlabeled_SelectedIndexChanged(object sender, EventArgs e)
        {
            LogCreator.unlabeled = (LogCreator.FormatUnlabeled)comboUnlabeled.SelectedIndex;
            UpdateSample();
        }

        private void comboStructure_SelectedIndexChanged(object sender, EventArgs e)
        {
            LogCreator.structure = (LogCreator.FormatStructure)comboStructure.SelectedIndex;
        }

        private bool ValidateFormat()
        {
            string[] tokens = textFormat.Text.ToLower().Split('%');

            // not valid if format has an odd amount of %s
            if (tokens.Length % 2 == 0) return false;

            for (int i = 1; i < tokens.Length; i += 2)
            {
                int indexOfColon = tokens[i].IndexOf(':');
                string kind = indexOfColon >= 0 ? tokens[i].Substring(0, indexOfColon) : tokens[i];

                // not valid if base token isn't one we know of
                if (!LogCreator.parameters.ContainsKey(kind)) return false;

                // not valid if parameter isn't an integer
                if (indexOfColon >= 0 && !int.TryParse(tokens[i].Substring(indexOfColon + 1), out int oof)) return false;
            }

            return true;
        }

        // https://stackoverflow.com/a/29679597
        private void UpdateSample()
        {
            // cheeky way of using the same methods for disassembling a different set of data :^)
            while (sampleTable.Count < 0x8000) sampleTable.Add(new ROMByte());

            using (MemoryStream mem = new MemoryStream())
            using (StreamWriter sw = new StreamWriter(mem))
            {
                List<ROMByte> tempTable = Data.GetTable();
                Data.ROMMapMode tempMode = Data.GetROMMapMode();
                Data.ROMSpeed tempSpeed = Data.GetROMSpeed();
                Dictionary<int, string> tempAlias = Data.GetAllLabels(), tempComment = Data.GetAllComments();
                LogCreator.FormatStructure tempStructure = LogCreator.structure;
                Data.Restore(sampleTable, Data.ROMMapMode.LoROM, Data.ROMSpeed.FastROM, sampleAlias, sampleComment);
                LogCreator.structure = LogCreator.FormatStructure.SingleFile;

                LogCreator.CreateLog(sw, StreamWriter.Null);

                Data.Restore(tempTable, tempMode, tempSpeed, tempAlias, tempComment);
                LogCreator.structure = tempStructure;

                sw.Flush();
                mem.Seek(0, SeekOrigin.Begin);

                textSample.Text = Encoding.UTF8.GetString(mem.ToArray(), 0, (int)mem.Length);
            }
        }

        // random sample code I made up; hopefully it shows a little bit of
        // everything so you can see how the settings will effect the output
        public static List<ROMByte> sampleTable = new List<ROMByte>
        {
            new ROMByte {Rom = 0x78, TypeFlag = Data.FlagType.Opcode, MFlag = true, XFlag = true, Point = Data.InOutPoint.InPoint},
            new ROMByte {Rom = 0xA9, TypeFlag = Data.FlagType.Opcode, MFlag = true, XFlag = true},
            new ROMByte {Rom = 0x01, TypeFlag = Data.FlagType.Operand},
            new ROMByte {Rom = 0x8D, TypeFlag = Data.FlagType.Opcode, MFlag = true, XFlag = true},
            new ROMByte {Rom = 0x0D, TypeFlag = Data.FlagType.Operand},
            new ROMByte {Rom = 0x42, TypeFlag = Data.FlagType.Operand},
            new ROMByte {Rom = 0x5C, TypeFlag = Data.FlagType.Opcode, MFlag = true, XFlag = true, Point = Data.InOutPoint.EndPoint},
            new ROMByte {Rom = 0x0A, TypeFlag = Data.FlagType.Operand},
            new ROMByte {Rom = 0x80, TypeFlag = Data.FlagType.Operand},
            new ROMByte {Rom = 0x80, TypeFlag = Data.FlagType.Operand},
            new ROMByte {Rom = 0xC2, TypeFlag = Data.FlagType.Opcode, MFlag = true, XFlag = true, Point = Data.InOutPoint.InPoint},
            new ROMByte {Rom = 0x30, TypeFlag = Data.FlagType.Operand},
            new ROMByte {Rom = 0xA9, TypeFlag = Data.FlagType.Opcode},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Operand},
            new ROMByte {Rom = 0x21, TypeFlag = Data.FlagType.Operand},
            new ROMByte {Rom = 0x5B, TypeFlag = Data.FlagType.Opcode},
            new ROMByte {Rom = 0x4B, TypeFlag = Data.FlagType.Opcode, DirectPage = 0x2100},
            new ROMByte {Rom = 0xAB, TypeFlag = Data.FlagType.Opcode, DirectPage = 0x2100},
            new ROMByte {Rom = 0xA2, TypeFlag = Data.FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x07, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xBF, TypeFlag = Data.FlagType.Opcode, Point = Data.InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x32, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x80, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x80, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x9F, TypeFlag = Data.FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x7E, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xCA, TypeFlag = Data.FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xCA, TypeFlag = Data.FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x10, TypeFlag = Data.FlagType.Opcode, Point = Data.InOutPoint.OutPoint, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xF4, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x64, TypeFlag = Data.FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x40, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x64, TypeFlag = Data.FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x41, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x64, TypeFlag = Data.FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x42, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x64, TypeFlag = Data.FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x43, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xAE, TypeFlag = Data.FlagType.Opcode, Point = Data.InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xFC, TypeFlag = Data.FlagType.Opcode, Point = Data.InOutPoint.OutPoint, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x3A, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x80, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x4C, TypeFlag = Data.FlagType.Opcode, Point = Data.InOutPoint.EndPoint, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xC0, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Data16Bit, Point = Data.InOutPoint.ReadPoint, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x08, TypeFlag = Data.FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x10, TypeFlag = Data.FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x20, TypeFlag = Data.FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x44, TypeFlag = Data.FlagType.Pointer16Bit, Point = Data.InOutPoint.ReadPoint, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x80, TypeFlag = Data.FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x7B, TypeFlag = Data.FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x80, TypeFlag = Data.FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x44, TypeFlag = Data.FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x81, TypeFlag = Data.FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xC4, TypeFlag = Data.FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x81, TypeFlag = Data.FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x0A, TypeFlag = Data.FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x82, TypeFlag = Data.FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x08, TypeFlag = Data.FlagType.Opcode, Point = Data.InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x8B, TypeFlag = Data.FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x4B, TypeFlag = Data.FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xAB, TypeFlag = Data.FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xE2, TypeFlag = Data.FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x20, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xC2, TypeFlag = Data.FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x10, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xA2, TypeFlag = Data.FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x1F, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xBD, TypeFlag = Data.FlagType.Opcode, MFlag = true, Point = Data.InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x5B, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x80, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x9D, TypeFlag = Data.FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x01, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xCA, TypeFlag = Data.FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x10, TypeFlag = Data.FlagType.Opcode, MFlag = true, Point = Data.InOutPoint.OutPoint, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xF7, TypeFlag = Data.FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xAB, TypeFlag = Data.FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x28, TypeFlag = Data.FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x60, TypeFlag = Data.FlagType.Opcode, Point = Data.InOutPoint.EndPoint, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x45, TypeFlag = Data.FlagType.Data8Bit, Point = Data.InOutPoint.ReadPoint, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x8D, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x69, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x83, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xB2, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x99, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x00, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x23, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x01, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xA3, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xF8, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x52, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x08, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xBB, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x29, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x5C, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x32, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xE7, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x88, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x3C, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x30, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x18, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x9A, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xB0, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x34, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x8C, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xDD, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x05, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0xB7, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x83, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x34, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            new ROMByte {Rom = 0x6D, TypeFlag = Data.FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
        };

        public static Dictionary<int, string> sampleAlias = new Dictionary<int, string>
        {
            { 0x00, "Emulation_RESET" },
            { 0x0A, "FastRESET" },
            { 0x32, "Test_Indices" },
            { 0x3A, "Pointer_Table" },
            { 0x44, "First_Routine" },
            { 0x5B, "Test_Data" }
        };

        public static Dictionary<int, string> sampleComment = new Dictionary<int, string>
        {
            { 0x03, "this sets FastROM" },
            { 0x0F, "direct page = $2100" },
            { 0x21, "clear APU regs" },
            { 0x44, "this routine copies Test_Data to $7E0100" }
        };
    }
}
