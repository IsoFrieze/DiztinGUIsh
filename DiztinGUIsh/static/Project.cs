using DiztinGUIsh.window;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using ExtendedXmlSerializer.ContentModel;
using ExtendedXmlSerializer.ContentModel.Format;

namespace DiztinGUIsh
{
    public static class Project
    {
        public const int HEADER_SIZE = 0x100;

        public static string currentFile = null, currentROMFile = null;
        public static bool unsavedChanges = false;
        public static string watermark = "DiztinGUIsh";

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

                currentROMFile = filename;

                ImportROMDialog import = new ImportROMDialog(rom);
                DialogResult result = import.ShowDialog();
                if (result == DialogResult.OK)
                {
                    Data.Inst.Initiate(rom, import.GetROMMapMode(), import.GetROMSpeed());
                    unsavedChanges = false;
                    currentFile = null;

                    AliasList.me.ResetDataGrid();
                    Dictionary<int, Data.AliasInfo> generatedLabels = import.GetGeneratedLabels();
                    if (generatedLabels.Count > 0)
                    {
                        foreach (KeyValuePair<int, Data.AliasInfo> pair in generatedLabels) Data.Inst.AddLabel(pair.Key, pair.Value, true);
                        unsavedChanges = true;
                    }

                    Dictionary<int, Data.FlagType> generatedFlags = import.GetHeaderFlags();
                    if (generatedFlags.Count > 0)
                    {
                        foreach (KeyValuePair<int, Data.FlagType> pair in generatedFlags) Data.Inst.SetFlag(pair.Key, pair.Value);
                        unsavedChanges = true;
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        private const int LATEST_FILE_FORMAT_VERSION = 2;

        /*sealed class RomByteSerializer : ISerializer<ROMByte>
        {
            public static RomByteSerializer Default { get; } = new RomByteSerializer();

            RomByteSerializer() { }

            public ROMByte Get(IFormatReader parameter)
            {
                //var parts = parameter.Content().Split('|');
                //var result = new ROMByte(parts[0], int.Parse(parts[1]));
                //return result;
                throw new NotImplementedException();
            }

            public void Write(IFormatWriter writer, ROMByte instance)
            {
                // use a custom formatter here to save space. there are a LOT of ROMBytes.
                // despite that we're still going for:
                // 1) text only for slightly human readability
                // 2) mergability in git/etc
                //
                // some of this can be unpacked further to increase readibility without
                // hurting the filesize too much. figure out what's useful.

                string flagTxt;
                switch (instance.TypeFlag)
                {
                    case Data.FlagType.Unreached: flagTxt = "?"; break;
                    case Data.FlagType.Opcode: flagTxt = "="; break;
                    case Data.FlagType.Operand: flagTxt = "-"; break;
                    case Data.FlagType.Graphics: flagTxt = "G"; break;
                    case Data.FlagType.Music: flagTxt = "M"; break;
                    case Data.FlagType.Empty: flagTxt = "E"; break;
                    case Data.FlagType.Text: flagTxt = "T"; break;

                    case Data.FlagType.Data8Bit: flagTxt = "1"; break;
                    case Data.FlagType.Data16Bit: flagTxt = "2"; break;
                    case Data.FlagType.Data24Bit: flagTxt = "3"; break;
                    case Data.FlagType.Data32Bit: flagTxt = "4"; break;

                    case Data.FlagType.Pointer16Bit: flagTxt = "p"; break;
                    case Data.FlagType.Pointer24Bit: flagTxt = "P"; break;
                    case Data.FlagType.Pointer32Bit: flagTxt = "X"; break;

                    default: throw new InvalidDataException("Unknown FlagType");
                }

                // max 8 bits
                byte otherFlags = (byte) (
                    (instance.XFlag ? 0 : 1) << 0 |   // 1 bit
                    (instance.MFlag ? 0 : 1) << 1 |   // 1 bit
                    (byte)instance.Point     << 2 |   // 4 bits
                    (byte)instance.Arch      << 6     // 2 bits
                );

                string data = 
                    flagTxt +
                    instance.DataBank.ToString("X2") +
                    instance.DirectPage.ToString("X4") +
                    otherFlags.ToString("X2");

                Debug.Assert(data.Length == 9);
                writer.Content(data);
            }
        }*/

        sealed class TableDataSerializer : ISerializer<TableData>
        {
            public static TableDataSerializer Default { get; } = new TableDataSerializer();

            TableDataSerializer() { }

            public TableData Get(IFormatReader parameter)
            {
                /*var parts = parameter.Content().Split('|');
                var result = new ROMByte(parts[0], int.Parse(parts[1]));
                return result;*/
                throw new NotImplementedException();
            }

            public void Write(IFormatWriter writer, TableData instance)
            {
                const bool compress_groupblock = true;

                var lines = new List<string>();
                foreach (var rb in instance.RomBytes)
                {
                    lines.Add(EncodeByte(rb));
                }

                var options = new List<string>();

                if (compress_groupblock)
                {
                    options.Add("compress_groupblocks");
                    ApplyCompression_GroupsBlocks(ref lines);
                }

                writer.Content($"\n{string.Join(",", options)}\n");

                foreach (var line in lines)
                {
                    writer.Content(line + "\n");
                }
            }

            public static void ApplyCompression_GroupsBlocks(ref List<string> lines)
            {
                if (lines.Count < 8)
                    return; // forget it, too small to care.

                var output = new List<string>();

                var lastline = lines[0];
                var consecutive = 1;

                // adjustable, just pick something > 8 or it's not worth the optimization.
                // we want to catch large consecutive blocks of data.
                const int min_number_repeats_before_we_bother = 8;

                for (var i = 0; i < lines.Count; ++i) {
                    var line = lines[i];

                    bool different = line != lastline;
                    if (!different)
                        consecutive++;

                    if (!different && i != lines.Count)
                        continue;

                    if (consecutive >= min_number_repeats_before_we_bother) {
                        // replace multiple repeated lines with one new statement
                        output.Add($"r {consecutive.ToString()} {lastline}");
                    } else {
                        // output 1 or more copies of the last line
                        // this is also how we print single lines too
                        output.AddRange(Enumerable.Repeat(lastline, consecutive).ToList());
                    }

                    lastline = line;
                    consecutive = 1;
                }

                lines = output;
            }

            public string EncodeByte(ROMByte instance)
            {
                // use a custom formatter here to save space. there are a LOT of ROMBytes.
                // despite that we're still going for:
                // 1) text only for slightly human readability
                // 2) mergability in git/etc
                //
                // some of this can be unpacked further to increase readability without
                // hurting the filesize too much. figure out what's useful.
                //
                // sorry, I know the encoding looks insane and weird and specific.  this reduced my
                // save file size from 42MB to less than 13MB

                // NOTE: must be uppercase letter or "=" or "-"
                // if you add things here, make sure you understand the compression settings above.
                string flagTxt;
                switch (instance.TypeFlag)
                {
                    case Data.FlagType.Unreached: flagTxt = "U"; break;

                    case Data.FlagType.Opcode: flagTxt = "-"; break;
                    case Data.FlagType.Operand: flagTxt = "="; break;

                    case Data.FlagType.Graphics: flagTxt = "G"; break;
                    case Data.FlagType.Music: flagTxt = "M"; break;
                    case Data.FlagType.Empty: flagTxt = "E"; break;
                    case Data.FlagType.Text: flagTxt = "T"; break;

                    case Data.FlagType.Data8Bit: flagTxt = "A"; break;
                    case Data.FlagType.Data16Bit: flagTxt = "B"; break;
                    case Data.FlagType.Data24Bit: flagTxt = "C"; break;
                    case Data.FlagType.Data32Bit: flagTxt = "D"; break;

                    case Data.FlagType.Pointer16Bit: flagTxt = "E"; break;
                    case Data.FlagType.Pointer24Bit: flagTxt = "F"; break;
                    case Data.FlagType.Pointer32Bit: flagTxt = "G"; break;

                    default: throw new InvalidDataException("Unknown FlagType");
                }

                // max 6 bits if we want to fit in 1 base64 ASCII digit
                byte otherFlags1 = (byte) (
                    (instance.XFlag ? 1 : 0) << 0 | // 1 bit
                    (instance.MFlag ? 1 : 0) << 1 | // 1 bit
                    (byte)instance.Point     << 2   // 4 bits
                );
                // reminder: when decoding, have to cut off all but the first 6 bits
                var o1_str = System.Convert.ToBase64String(new byte[] { otherFlags1 });
                Debug.Assert(o1_str.Length == 4);
                o1_str = o1_str.Remove(1);
                
                if (!instance.XFlag && !instance.MFlag && instance.Point == 0)
                    Debug.Assert(o1_str == "A"); // sanity

                // dumbest thing in the entire world.
                // the more zeroes we output, the more compressed we get.
                // let's swap "A" (index 0) for "0" (index 52).
                // if you got here after being really fucking confused about why
                // your Base64 encoding algo wasn't working, then I owe you a beer. super-sorry.
                // you are now allowed to flip your desk over. say it with me
                // "Damnit Dom!!! Y U DO THIS"
                if (o1_str == "A") 
                    o1_str = "0";       // get me that sweet, sweet zero
                else if (o1_str == "0") 
                    o1_str = "A";

                // this is basically going to be "0" almost 100% of the time.
                // we'll put it on the end of the string so it's most likely not output
                byte otherFlags2 = (byte)(
                    (byte)instance.Arch << 0 // 2 bits
                );
                var o2_str = otherFlags2.ToString("X1"); Debug.Assert(o2_str.Length == 1);

                // ordering: put DB and D on the end, they're likely to be zero and compressible
                string data =
                    flagTxt + // 1
                    o1_str +  // 1
                    instance.DataBank.ToString("X2") +  // 2
                    instance.DirectPage.ToString("X4") + // 4
                    o2_str; // 1

                Debug.Assert(data.Length == 9);

                // light compression: chop off any trailing zeroes.
                // this alone saves a giant amount of space.
                data = data.TrimEnd(new Char[] {'0'});

                // future compression but dilutes readability:
                // if type is opcode or operand, combine 

                return data;
            }
        }

        public static void SaveProject(string filename)
        {
            var serializer = new ConfigurationContainer()
                .Type<TableData>().Register().Serializer().Using(TableDataSerializer.Default)
                .UseOptimizedNamespaces()   //If you want to have all namespaces in root element
                .Create();

            var xml = serializer.Serialize(
                new XmlWriterSettings {
                    Indent = true
                }, Data.Inst);

            File.WriteAllText(filename + ".xml", xml);
        }

        public static void SaveProjectORIG(string filename)
        {
            try
            {
                const int versionToSave = LATEST_FILE_FORMAT_VERSION;

                byte[] data = SaveVersion(versionToSave);
                byte[] everything = new byte[HEADER_SIZE + data.Length];
                everything[0] = versionToSave;
                Util.StringToByteArray(watermark).CopyTo(everything, 1);
                data.CopyTo(everything, HEADER_SIZE);

                if (!IsUncompressedProject(filename)) everything = TryZip(everything);

                File.WriteAllBytes(filename, everything);
                unsavedChanges = false;
                currentFile = filename;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static byte[] SaveVersion(int version)
        {
            void SaveStringToBytes(string str, List<byte> bytes)
            {
                // TODO: combine with Util.StringToByteArray() probably.
                if (str != null)
                {
                    foreach (var c in str)
                    {
                        bytes.Add((byte)c);
                    }
                }
                bytes.Add(0);
            }

            if (version < 1 || version > LATEST_FILE_FORMAT_VERSION)
            {
                throw new ArgumentException($"Saving: Invalid save version requested for saving: {version}.");
            }

            int size = Data.Inst.GetROMSize();
            byte[] romSettings = new byte[31];
            romSettings[0] = (byte)Data.Inst.GetROMMapMode();
            romSettings[1] = (byte)Data.Inst.GetROMSpeed();
            Util.IntegerIntoByteArray(size, romSettings, 2);
            for (int i = 0; i < 0x15; i++) romSettings[6 + i] = (byte)Data.Inst.GetROMByte(Util.ConvertSNEStoPC(0xFFC0 + i));
            for (int i = 0; i < 4; i++) romSettings[27 + i] = (byte)Data.Inst.GetROMByte(Util.ConvertSNEStoPC(0xFFDC + i));

            // TODO put selected offset in save file

            List<byte> label = new List<byte>(), comment = new List<byte>();
            var all_labels = Data.Inst.GetAllLabels();
            var all_comments = Data.Inst.GetAllComments();

            Util.IntegerIntoByteList(all_labels.Count, label);
            foreach (var pair in all_labels)
            {
                Util.IntegerIntoByteList(pair.Key, label);

                SaveStringToBytes(pair.Value.name, label);
                if (version >= 2)
                {
                    SaveStringToBytes(pair.Value.comment, label);
                }
            }

            Util.IntegerIntoByteList(all_comments.Count, comment);
            foreach (KeyValuePair<int, string> pair in all_comments)
            {
                Util.IntegerIntoByteList(pair.Key, comment);
                SaveStringToBytes(pair.Value, comment);
            }

            byte[] romLocation = Util.StringToByteArray(currentROMFile);

            byte[] data = new byte[romSettings.Length + romLocation.Length + 8 * size + label.Count + comment.Count];
            romSettings.CopyTo(data, 0);
            for (int i = 0; i < romLocation.Length; i++) data[romSettings.Length + i] = romLocation[i];

            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + i] = (byte)Data.Inst.GetDataBank(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + size + i] = (byte)Data.Inst.GetDirectPage(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + 2 * size + i] = (byte)(Data.Inst.GetDirectPage(i) >> 8);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + 3 * size + i] = (byte)(Data.Inst.GetXFlag(i) ? 1 : 0);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + 4 * size + i] = (byte)(Data.Inst.GetMFlag(i) ? 1 : 0);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + 5 * size + i] = (byte)Data.Inst.GetFlag(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + 6 * size + i] = (byte)Data.Inst.GetArchitechture(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + 7 * size + i] = (byte)Data.Inst.GetInOutPoint(i);
            // ???
            label.CopyTo(data, romSettings.Length + romLocation.Length + 8 * size);
            comment.CopyTo(data, romSettings.Length + romLocation.Length + 8 * size + label.Count);
            // ???

            return data;
        }

        public static bool TryOpenProject(string filename, OpenFileDialog open)
        {
            try
            {
                byte[] data = File.ReadAllBytes(filename);

                if (!IsUncompressedProject(filename)) data = TryUnzip(data);

                for (int i = 0; i < watermark.Length; i++)
                {
                    if (data[i + 1] != (byte)watermark[i])
                    {
                        throw new Exception("This is not a valid DiztinGUIsh file!");
                    }
                }

                byte version = data[0];
                OpenProject(version, data, open);

                unsavedChanges = false;
                currentFile = filename;
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error opening project file", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private delegate int AddressConverter(int address);

        private static void OpenProject(int version, byte[] unzipped, OpenFileDialog open)
        {
            if (version > LATEST_FILE_FORMAT_VERSION)
            {
                throw new ArgumentException("This DiztinGUIsh file uses a newer file format! You'll need to download the newest version of DiztinGUIsh to open it.");
            }
            else if (version != LATEST_FILE_FORMAT_VERSION)
            {
                MessageBox.Show(
                    "This project file is in an older format.\n" +
                    "You may want to back up your work or 'Save As' in case the conversion goes wrong.\n" +
                    "The project file will be untouched until it is saved again.",
                    "Project File Out of Date", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            
            if (version < 0)
            {
                throw new ArgumentException($"Invalid project file version detected: {version}.");
            }

            // version 0 needs to convert PC to SNES for some addresses
            AddressConverter converter = address => address;
            if (version == 0)
                converter = Util.ConvertPCtoSNES;

            Data.ROMMapMode mode = (Data.ROMMapMode)unzipped[HEADER_SIZE];
            Data.ROMSpeed speed = (Data.ROMSpeed)unzipped[HEADER_SIZE + 1];
            int size = Util.ByteArrayToInteger(unzipped, HEADER_SIZE + 2);
            string romName = "", romLocation = "";
            byte[] rom;

            int pointer = HEADER_SIZE + 6;
            for (int i = 0; i < 0x15; i++) romName += (char) unzipped[pointer++];
            int checksums = Util.ByteArrayToInteger(unzipped, pointer);
            pointer += 4;
            while (unzipped[pointer] != 0) romLocation += (char) unzipped[pointer++];
            pointer++;

            if (ValidateROM(romLocation, romName, checksums, mode, out rom, open))
            {
                Data.Inst.Initiate(rom, mode, speed);

                for (int i = 0; i < size; i++) Data.Inst.SetDataBank(i, unzipped[pointer + i]);
                for (int i = 0; i < size; i++) Data.Inst.SetDirectPage(i, unzipped[pointer + size + i] | (unzipped[pointer + 2 * size + i] << 8));
                for (int i = 0; i < size; i++) Data.Inst.SetXFlag(i, unzipped[pointer + 3 * size + i] != 0);
                for (int i = 0; i < size; i++) Data.Inst.SetMFlag(i, unzipped[pointer + 4 * size + i] != 0);
                for (int i = 0; i < size; i++) Data.Inst.SetFlag(i, (Data.FlagType)unzipped[pointer + 5 * size + i]);
                for (int i = 0; i < size; i++) Data.Inst.SetArchitechture(i, (Data.Architechture)unzipped[pointer + 6 * size + i]);
                for (int i = 0; i < size; i++) Data.Inst.SetInOutPoint(i, (Data.InOutPoint)unzipped[pointer + 7 * size + i]);
                pointer += 8 * size;

                AliasList.me.ResetDataGrid();
                ReadAliases(unzipped, ref pointer, converter, version >= 2);

                ReadComments(unzipped, ref pointer, converter);
            }
            else
            {
                throw new Exception("Couldn't open the ROM file!");
            }
        }

        // TODO: refactor ReadComments and ReadAliases into one generic list-reading function

        private static void ReadComments(byte[] unzipped, ref int pointer, AddressConverter converter)
        {
            var count = Util.ByteArrayToInteger(unzipped, pointer);
            pointer += 4;

            for (var i = 0; i < count; i++)
            {
                int offset = converter(Util.ByteArrayToInteger(unzipped, pointer));
                pointer += 4;

                var comment = Util.ReadZipString(unzipped, ref pointer);

                Data.Inst.AddComment(offset, comment, true);
            }
        }

        private static void ReadAliases(byte[] unzipped, ref int pointer, AddressConverter converter, bool readAliasComments)
        {
            int count = Util.ByteArrayToInteger(unzipped, pointer);
            pointer += 4;

            for (int i = 0; i < count; i++)
            {
                int offset = converter(Util.ByteArrayToInteger(unzipped, pointer));
                pointer += 4;

                var aliasInfo = new Data.AliasInfo {
                    name = Util.ReadZipString(unzipped, ref pointer), 
                    comment = readAliasComments ? Util.ReadZipString(unzipped, ref pointer) : "",
                };
                aliasInfo.CleanUp();

                Data.Inst.AddLabel(offset, aliasInfo, true);
            }
        }

        private static bool ValidateROM(string filename, string romName, int checksums, Data.ROMMapMode mode, out byte[] rom, OpenFileDialog open)
        {
            bool validFile = false, matchingROM = false;
            rom = null;
            open.InitialDirectory = currentFile;

            while (!matchingROM)
            {
                string error = null;
                matchingROM = false;

                while (!validFile)
                {
                    error = null;
                    validFile = false;

                    try
                    {
                        byte[] smc = File.ReadAllBytes(filename);
                        rom = new byte[smc.Length & 0x7FFFFC00];

                        if ((smc.Length & 0x3FF) == 0x200) for (int i = 0; i < rom.Length; i++) rom[i] = smc[i + 0x200];
                        else if ((smc.Length & 0x3FF) != 0) error = "The linked ROM has an unusual size. It can't be opened.";
                        else rom = smc;

                        if (error == null) validFile = true;
                    }
                    catch (Exception)
                    {
                        error = string.Format("The linked ROM file '{0}' couldn't be found.", filename);
                    }

                    if (!validFile)
                    {
                        DialogResult result = MessageBox.Show(string.Format("{0} Link a new ROM now?", error), "Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                        if (result == DialogResult.No) return false;
                        result = open.ShowDialog();
                        if (result == DialogResult.OK) filename = open.FileName;
                        else return false;
                    }
                }

                validFile = false;
                int offset = Data.Inst.GetRomSettingOffset(mode);
                if (rom.Length <= offset + 10) error = "The linked ROM is too small. It can't be opened.";

                string myName = "";
                for (int i = 0; i < 0x15; i++) myName += (char)rom[offset - 0x15 + i];
                int myChecksums = Util.ByteArrayToInteger(rom, offset + 7);

                if (myName != romName) error = string.Format("The linked ROM's internal name '{0}' doesn't match the project's internal name of '{1}'.", myName, romName);
                else if (checksums != myChecksums) error = string.Format("The linked ROM's checksums '{0:X8}' don't match the project's checksums of '{1:X8}'.", myChecksums, checksums);

                if (error == null) matchingROM = true;

                if (!matchingROM)
                {
                    DialogResult result = MessageBox.Show(string.Format("{0} Link a new ROM now?", error), "Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                    if (result == DialogResult.No) return false;
                    result = open.ShowDialog();
                    if (result == DialogResult.OK) filename = open.FileName;
                    else return false;
                }
            }

            if (currentROMFile != filename)
            {
                currentROMFile = filename;
                unsavedChanges = true;
            }
            return true;
        }

        private static bool IsUncompressedProject(string filename)
        {
            return Path.GetExtension(filename).Equals(".dizraw", StringComparison.InvariantCultureIgnoreCase);
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
            catch (Exception)
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
            catch (Exception)
            {
                return null;
            }
        }
    }
}
