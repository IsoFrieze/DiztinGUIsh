using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiztinGUIsh
{
    public static class Project
    {
        public const int HEADER_SIZE = 0x100;

        public static string currentFile = null;
        public static bool unsavedChanges = false;
        public static byte[] watermark = new byte[] { 0x44, 0x69, 0x7A, 0x74, 0x69, 0x6E, 0x47, 0x55, 0x49, 0x73, 0x68 };

        public static bool NewProject(string filename)
        {
            try
            {
                byte[] smc = File.ReadAllBytes(filename);
                byte[] rom = new byte[smc.Length & 0x7FFFFC00];

                if ((smc.Length & 0x3FF) == 0x200) for (int i = 0; i < rom.Length; i++) rom[i] = smc[i + 0x200];
                else if ((smc.Length & 0x3FF) != 0) throw new Exception("This ROM has an unusual size. It can't be opened.");
                else rom = smc;

                if (rom.Length < 0x8000) throw new Exception("This ROM is too small. It can't be opened.");

                ImportROMDialog import = new ImportROMDialog(rom);
                DialogResult result = import.ShowDialog();
                if (result == DialogResult.OK)
                {
                    Data.Initiate(rom, import.GetROMMapMode(), import.GetROMSpeed());
                    unsavedChanges = false;
                    return true;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        public static void SaveProject(string filename)
        {
            try
            {
                byte[] data = SaveVersion0();
                byte[] everything = new byte[HEADER_SIZE + data.Length];
                everything[0] = 0; // version
                watermark.CopyTo(everything, 1);
                data.CopyTo(everything, HEADER_SIZE);

                File.WriteAllBytes(filename, TryZip(everything));
                unsavedChanges = false;
                currentFile = filename;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static byte[] SaveVersion0()
        {
            int size = Data.GetROMSize();
            byte[] romSettings = new byte[6];
            romSettings[0] = (byte)Data.GetROMMapMode();
            romSettings[1] = (byte)Data.GetROMSpeed();
            Util.IntegerIntoByteArray(size, romSettings, 2);

            List<byte> label = new List<byte>(), comment = new List<byte>();
            Dictionary<int, string> all_labels = Data.GetAllLabels(), all_comments = Data.GetAllComments();

            Util.IntegerIntoByteList(all_labels.Count, label);
            foreach (KeyValuePair<int, string> pair in all_labels)
            {
                Util.IntegerIntoByteList(pair.Key, label);
                for (int i = 0; i < pair.Value.Length; i++) label.Add((byte)pair.Value[i]);
                label.Add(0);
            }

            Util.IntegerIntoByteList(all_comments.Count, comment);
            foreach (KeyValuePair<int, string> pair in all_comments)
            {
                Util.IntegerIntoByteList(pair.Key, comment);
                for (int i = 0; i < pair.Value.Length; i++) comment.Add((byte)pair.Value[i]);
                comment.Add(0);
            }

            byte[] data = new byte[romSettings.Length + 9 * size + label.Count + comment.Count];
            romSettings.CopyTo(data, 0);
            for (int i = 0; i < size; i++) data[romSettings.Length + i] = (byte)Data.GetROMByte(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + size + i] = (byte)Data.GetDataBank(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + 2 * size + i] = (byte)Data.GetDirectPage(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + 3 * size + i] = (byte)(Data.GetDirectPage(i) >> 8);
            for (int i = 0; i < size; i++) data[romSettings.Length + 4 * size + i] = (byte)(Data.GetXFlag(i) ? 1 : 0);
            for (int i = 0; i < size; i++) data[romSettings.Length + 5 * size + i] = (byte)(Data.GetMFlag(i) ? 1 : 0);
            for (int i = 0; i < size; i++) data[romSettings.Length + 6 * size + i] = (byte)Data.GetFlag(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + 7 * size + i] = (byte)Data.GetArchitechture(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + 8 * size + i] = (byte)Data.GetInOutPoint(i);
            label.CopyTo(data, romSettings.Length + 9 * size);
            comment.CopyTo(data, romSettings.Length + 9 * size + label.Count);

            return data;
        }

        public static bool TryOpenProject(string filename)
        {
            try
            {
                byte[] raw = File.ReadAllBytes(filename);
                byte[] unzipped = TryUnzip(raw);
                File.WriteAllBytes("C:/Users/Alex/Desktop/test.bin", unzipped);

                for (int i = 0; i < watermark.Length; i++)
                {
                    if (unzipped[i + 1] != watermark[i])
                    {
                        throw new Exception("This is not a valid DiztinGUIsh file!");
                    }
                }

                byte version = unzipped[0];

                switch (version)
                {
                    case 0: OpenVersion0(unzipped); break;
                    default: throw new Exception("This is not a valid DiztinGUIsh file!");
                }

                unsavedChanges = false;
                currentFile = filename;
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private static void OpenVersion0(byte[] unzipped)
        {
            Data.ROMMapMode mode = (Data.ROMMapMode)unzipped[HEADER_SIZE];
            Data.ROMSpeed speed = (Data.ROMSpeed)unzipped[HEADER_SIZE + 1];
            int size = Util.ByteArrayToInteger(unzipped, HEADER_SIZE + 2);
            
            byte[] rom = new byte[size];
            for (int i = 0; i < size; i++) rom[i] = unzipped[HEADER_SIZE + 6 + i];

            Data.Initiate(rom, mode, speed);

            for (int i = 0; i < size; i++) Data.SetDataBank(i, unzipped[HEADER_SIZE + 6 + size + i]);
            for (int i = 0; i < size; i++) Data.SetDirectPage(i, unzipped[HEADER_SIZE + 6 + 2 * size + i] | (unzipped[HEADER_SIZE + 6 + 3 * size + i] << 8));
            for (int i = 0; i < size; i++) Data.SetXFlag(i, unzipped[HEADER_SIZE + 6 + 4 * size + i] != 0);
            for (int i = 0; i < size; i++) Data.SetMFlag(i, unzipped[HEADER_SIZE + 6 + 5 * size + i] != 0);
            for (int i = 0; i < size; i++) Data.SetFlag(i, (Data.FlagType)unzipped[HEADER_SIZE + 6 + 6 * size + i]);
            for (int i = 0; i < size; i++) Data.SetArchitechture(i, (Data.Architechture)unzipped[HEADER_SIZE + 6 + 7 * size + i]);
            for (int i = 0; i < size; i++) Data.SetInOutPoint(i, (Data.InOutPoint)unzipped[HEADER_SIZE + 6 + 8 * size + i]);

            int pointer = HEADER_SIZE + 6 + 9 * size;
            int label_count = Util.ByteArrayToInteger(unzipped, pointer);
            pointer += 4;

            for (int i = 0; i < label_count; i++)
            {
                int offset = Util.ByteArrayToInteger(unzipped, pointer);
                pointer += 4;

                string label = "";
                while (unzipped[pointer] != 0) label += (char)unzipped[pointer++];
                pointer++;

                Data.AddLabel(offset, label);
            }

            int comment_count = Util.ByteArrayToInteger(unzipped, pointer);
            pointer += 4;

            for (int i = 0; i < comment_count; i++)
            {
                int offset = Util.ByteArrayToInteger(unzipped, pointer);
                pointer += 4;

                string comment = "";
                while (unzipped[pointer] != 0) comment += (char)unzipped[pointer++];
                pointer++;

                Data.AddComment(offset, comment);
            }
        }

        // https://stackoverflow.com/questions/33119119/unzip-byte-array-in-c-sharp
        private static byte[] TryUnzip(byte[] data)
        {
            try
            {
                using (MemoryStream comp = new MemoryStream(data))
                using (GZipStream gzip = new GZipStream(comp, CompressionMode.Decompress))
                using (MemoryStream res = new MemoryStream())
                {
                    gzip.CopyTo(res);
                    return res.ToArray();
                }
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private static byte[] TryZip(byte[] data)
        {
            try
            {
                using (MemoryStream comp = new MemoryStream())
                using (GZipStream gzip = new GZipStream(comp, CompressionMode.Compress))
                {
                    gzip.Write(data, 0, data.Length);
                    gzip.Close();
                    return comp.ToArray();
                }
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}
