using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiztinGUIsh.window;

namespace DiztinGUIsh.loadsave.binary_serializer_old
{
    internal class BinarySerializer : ProjectSerializer
    {
        public const int HEADER_SIZE = 0x100;
        private const int LATEST_FILE_FORMAT_VERSION = 2;

        public static bool IsBinaryFileFormat(byte[] data)
        {
            for (var i = 0; i < Watermark.Length; i++) {
                if (data[i + 1] != (byte) Watermark[i])
                    return false;
            }
            return true;
        }

        public override byte[] Save(Project project)
        {
            const int versionToSave = LATEST_FILE_FORMAT_VERSION;
            byte[] data = SaveVersion(project, versionToSave);

            byte[] everything = new byte[HEADER_SIZE + data.Length];
            everything[0] = versionToSave;
            Util.StringToByteArray(Watermark).CopyTo(everything, 1);
            data.CopyTo(everything, HEADER_SIZE);

            return data;
        }

        public override Project Load(byte[] data)
        {
            if (!IsBinaryFileFormat(data))
                throw new InvalidDataException($"This is not a binary serialized project file!");

            byte version = data[0];
            ValidateProjectFileVersion(version);

            var project = new Project
            {
                Data = new Data()
            };

            // version 0 needs to convert PC to SNES for some addresses
            Util.AddressConverter converter = address => address;
            if (version == 0)
                converter = project.Data.ConvertPCtoSNES;

            // read mode, speed, size
            project.Data.RomMapMode = (Data.ROMMapMode)data[HEADER_SIZE];
            project.Data.RomSpeed = (Data.ROMSpeed)data[HEADER_SIZE + 1];
            var size = Util.ByteArrayToInteger(data, HEADER_SIZE + 2);

            // read internal title
            var pointer = HEADER_SIZE + 6;
            for (var i = 0; i < 0x15; i++) 
                project.InternalRomGameName += (char)data[pointer++];

            // read checksums
            project.InternalCheckSum = Util.ByteArrayToInteger(data, pointer);
            pointer += 4;

            // read full filepath to the ROM .sfc file
            while (data[pointer] != 0)
                project.AttachedRomFilename += (char)data[pointer++];
            pointer++;

            var rom = project.ReadFromOriginalRom();
            if (rom == null)
                throw new Exception("Couldn't open the ROM file!");

            project.Data.Initiate(rom, project.Data.RomMapMode, project.Data.RomSpeed);

            for (int i = 0; i < size; i++) project.Data.SetDataBank(i, data[pointer + i]);
            for (int i = 0; i < size; i++) project.Data.SetDirectPage(i, data[pointer + size + i] | (data[pointer + 2 * size + i] << 8));
            for (int i = 0; i < size; i++) project.Data.SetXFlag(i, data[pointer + 3 * size + i] != 0);
            for (int i = 0; i < size; i++) project.Data.SetMFlag(i, data[pointer + 4 * size + i] != 0);
            for (int i = 0; i < size; i++) project.Data.SetFlag(i, (Data.FlagType)data[pointer + 5 * size + i]);
            for (int i = 0; i < size; i++) project.Data.SetArchitechture(i, (Data.Architecture)data[pointer + 6 * size + i]);
            for (int i = 0; i < size; i++) project.Data.SetInOutPoint(i, (Data.InOutPoint)data[pointer + 7 * size + i]);
            pointer += 8 * size;

            ReadLabels(project, data, ref pointer, converter, version >= 2);
            ReadComments(project, data, ref pointer, converter);

            project.UnsavedChanges = false;

            return project;
        }

        private static void SaveStringToBytes(string str, List<byte> bytes)
        {
            // TODO: combine with Util.StringToByteArray() probably.
            if (str != null) {
                foreach (var c in str) {
                    bytes.Add((byte)c);
                }
            }
            bytes.Add(0);
        }

        private byte[] SaveVersion(Project project, int version)
        {
            ValidateSaveVersion(version);

            int size = project.Data.GetROMSize();
            byte[] romSettings = new byte[31];

            // save these two
            romSettings[0] = (byte)project.Data.GetROMMapMode();
            romSettings[1] = (byte)project.Data.GetROMSpeed();

            // save the size, 4 bytes
            Util.IntegerIntoByteArray(size, romSettings, 2);

            var romName = project.Data.GetRomNameFromRomBytes();
            romName.ToCharArray().CopyTo(romSettings, 6);

            var romChecksum = project.Data.GetRomCheckSumsFromRomBytes();
            BitConverter.GetBytes(romChecksum).CopyTo(romSettings, 27);

            // TODO put selected offset in save file

            // save all labels ad comments
            List<byte> label = new List<byte>(), comment = new List<byte>();
            var all_labels = project.Data.Labels;
            var all_comments = project.Data.Comments;

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

            // save current Rom full path - "c:\whatever\someRom.sfc"
            byte[] romLocation = Util.StringToByteArray(project.AttachedRomFilename);

            byte[] data = new byte[romSettings.Length + romLocation.Length + 8 * size + label.Count + comment.Count];
            romSettings.CopyTo(data, 0);
            for (int i = 0; i < romLocation.Length; i++) data[romSettings.Length + i] = romLocation[i];

            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + i] = (byte)project.Data.GetDataBank(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + size + i] = (byte)project.Data.GetDirectPage(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + 2 * size + i] = (byte)(project.Data.GetDirectPage(i) >> 8);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + 3 * size + i] = (byte)(project.Data.GetXFlag(i) ? 1 : 0);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + 4 * size + i] = (byte)(project.Data.GetMFlag(i) ? 1 : 0);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + 5 * size + i] = (byte)project.Data.GetFlag(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + 6 * size + i] = (byte)project.Data.GetArchitecture(i);
            for (int i = 0; i < size; i++) data[romSettings.Length + romLocation.Length + 7 * size + i] = (byte)project.Data.GetInOutPoint(i);
            // ???
            label.CopyTo(data, romSettings.Length + romLocation.Length + 8 * size);
            comment.CopyTo(data, romSettings.Length + romLocation.Length + 8 * size + label.Count);
            // ???

            return data;
        }

        private static void ValidateSaveVersion(int version) {
            if (version < 1 || version > LATEST_FILE_FORMAT_VERSION) {
                throw new ArgumentException($"Saving: Invalid save version requested for saving: {version}.");
            }
        }

        private static void ValidateProjectFileVersion(int version)
        {
            if (version > LATEST_FILE_FORMAT_VERSION)
            {
                throw new ArgumentException(
                    "This DiztinGUIsh file uses a newer file format! You'll need to download the newest version of DiztinGUIsh to open it.");
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
        }

        private void ReadComments(Project project, byte[] bytes, ref int pointer, Util.AddressConverter converter)
        {
            const int stringsPerEntry = 1;
            pointer += Util.ReadStringsTable(bytes, pointer, stringsPerEntry, converter, 
                (int offset, string[] strings) =>
            {
                Debug.Assert(strings.Length == 1);
                project.Data.AddComment(offset, strings[0], true);
            });
        }

        private void ReadLabels(Project project, byte[] bytes, ref int pointer, Util.AddressConverter converter, bool readAliasComments)
        {
            var stringsPerEntry = readAliasComments ? 2 : 1;
            pointer += Util.ReadStringsTable(bytes, pointer, stringsPerEntry, converter,
                (int offset, string[] strings) =>
                {
                    Debug.Assert(strings.Length == stringsPerEntry);
                    var label = new Label
                    {
                        name = strings[0],
                        comment = strings.ElementAtOrDefault(1)
                    };
                    label.CleanUp();
                    project.Data.AddLabel(offset, label, true);
                });
        }
    }
}
