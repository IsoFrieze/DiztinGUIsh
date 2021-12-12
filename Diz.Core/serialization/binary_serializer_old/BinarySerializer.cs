using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Diz.Core.model;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.model.snes;
using Diz.Core.util;

// IMPORTANT NOTE:
// This serializer is compact, but it's deprecated in favor of the XML serializer, which is way easier
// to make changes to and deal with backwards compatibility.
//
// This is only here for loading older files saved in this format, it shouldn't be used for anything new going forward.

namespace Diz.Core.serialization.binary_serializer_old
{
    internal class BinarySerializer : ProjectSerializer
    {
        public const int HeaderSize = 0x100;
        private const int LatestFileFormatVersion = 2;

        public static bool IsBinaryFileFormat(byte[] data)
        {
            for (var i = 0; i < DizWatermark.Length; i++) {
                if (data[i + 1] != (byte) DizWatermark[i])
                    return false;
            }
            return true;
        }

        public override byte[] Save(Project project)
        {
            const int versionToSave = LatestFileFormatVersion;
            var data = SaveVersion(project, versionToSave);

            var everything = new byte[HeaderSize + data.Length];
            everything[0] = versionToSave;
            ByteUtil.StringToNullTermByteArray(DizWatermark).CopyTo(everything, 1);
            data.CopyTo(everything, HeaderSize);

            return data;
        }

        public override (ProjectXmlSerializer.Root xmlRoot, string warning) Load(byte[] rawBytes)
        {
            var (project, warning, version) = LoadProject(rawBytes);

            // the binary serializer versions start at "1" and go up to a max of 99.
            // XML serializers pick up at 100 and go upwards from there.
            Debug.Assert(version < ProjectXmlSerializer.FirstSaveFormatVersion);
            
            // have to pack this into the new XML root structure.
            return (new ProjectXmlSerializer.Root
            {
                Project = project,
                SaveVersion = version,
                Watermark = DizWatermark,
            }, warning);
        }
        private (Project project, string warning, byte version) LoadProject(byte[] data)
        {
            if (!IsBinaryFileFormat(data))
                throw new InvalidDataException("This is not a binary serialized project file!");

            var version = data[0];
            ValidateProjectFileVersion(version);

            var project = new Project {
                Data = new Data()
            };

            #if DIZ_3_BRANCH
            project.Session = new ProjectSession(project)
            {
                UnsavedChanges = false
            };
            #endif

            // version 0 needs to convert PC to SNES for some addresses
            ByteUtil.AddressConverter converter = address => address;
            if (version == 0)
                converter = project.Data.ConvertPCtoSnes;

            // read mode, speed, size
            var mode = (RomMapMode) data[HeaderSize];
            var speed = (RomSpeed) data[HeaderSize + 1];
            
            #if !DIZ_3_BRANCH
            project.Data.RomMapMode = mode;
            project.Data.RomSpeed = speed;
            #endif
            
            var size = ByteUtil.ConvertByteArrayToInt32(data, HeaderSize + 2);

            // read internal title
            var pointer = HeaderSize + 6;
            project.InternalRomGameName = ByteUtil.ReadStringFromByteArray(data, pointer, RomUtil.LengthOfTitleName);
            pointer += RomUtil.LengthOfTitleName;

            // read checksums
            project.InternalCheckSum = ByteUtil.ConvertByteArrayToUInt32(data, pointer);
            pointer += 4;

            // read full filepath to the ROM .sfc file
            while (data[pointer] != 0)
                project.AttachedRomFilename += (char) data[pointer++];
            pointer++;

            #if DIZ_3_BRANCH
            project.Data.InitializeEmptyRomMapping(size, mode, speed);
            #else
            project.Data.RomBytes.Create(size);
            #endif

            for (int i = 0; i < size; i++) project.Data.SetDataBank(i, data[pointer + i]);
            for (int i = 0; i < size; i++)
                project.Data.SetDirectPage(i, data[pointer + size + i] | (data[pointer + 2 * size + i] << 8));
            for (int i = 0; i < size; i++) project.Data.SetXFlag(i, data[pointer + 3 * size + i] != 0);
            for (int i = 0; i < size; i++) project.Data.SetMFlag(i, data[pointer + 4 * size + i] != 0);
            for (int i = 0; i < size; i++) project.Data.SetFlag(i, (FlagType) data[pointer + 5 * size + i]);
            for (int i = 0; i < size; i++) project.Data.SetArchitecture(i, (Architecture) data[pointer + 6 * size + i]);
            for (int i = 0; i < size; i++) project.Data.SetInOutPoint(i, (InOutPoint) data[pointer + 7 * size + i]);
            pointer += 8 * size;

            ReadLabels(project, data, ref pointer, converter, version >= 2);
            ReadComments(project, data, ref pointer, converter);

            #if !DIZ_3_BRANCH
            project.UnsavedChanges = false;
            #endif

            var warning = "";
            if (version != LatestFileFormatVersion)
            {
                warning = "This project file is in an older format.\n" +
                              "You may want to back up your work or 'Save As' in case the conversion goes wrong.\n" +
                              "The project file will be untouched until it is saved again.";
            }

            return (project, warning, version);
        }

        #if ALLOW_OLD_SAVE_FORMATS
        private static void SaveStringToBytes(string str, ICollection<byte> bytes)
        {
            // TODO: combine with Util.StringToByteArray() probably.
            if (str != null) {
                foreach (var c in str) {
                    bytes.Add((byte)c);
                }
            }
            bytes.Add(0);
        }
        #endif

        private void VoidTheWarranty()
        {
            // comment this out only if you are an expert and know what you're doing. Binary serialization is deprecated.
            //
            // How did you even get here, dawg? #yolo
            throw new NotSupportedException("Binary serializer saving is OLD, please use the XML serializer instead.");
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private byte[] SaveVersion(Project project, int version)
        {
            VoidTheWarranty();
            
            #if ALLOW_OLD_SAVE_FORMATS
            ValidateSaveVersion(version);

            int size = project.Data.GetRomSize();
            byte[] romSettings = new byte[31];

            // save these two
            romSettings[0] = (byte)project.Data.RomMapMode;
            romSettings[1] = (byte)project.Data.RomSpeed;

            // save the size, 4 bytes
            ByteUtil.IntegerIntoByteArray((uint)size, romSettings, 2);

            var romName = project.Data.CartridgeTitleName;
            romName.ToCharArray().CopyTo(romSettings, 6);

            var romChecksum = project.Data.RomCheckSumsFromRomBytes;
            BitConverter.GetBytes(romChecksum).CopyTo(romSettings, 27);

            // TODO put selected offset in save file

            // save all labels ad comments
            List<byte> label = new(), comment = new();
            var allLabels = project.Data.Labels;
            var allComments = project.Data.Comments;

            ByteUtil.AppendIntegerToByteList((uint)allLabels.Count, label);
            foreach (var pair in allLabels)
            {
                ByteUtil.AppendIntegerToByteList((uint)pair.Key, label);

                SaveStringToBytes(pair.Value.Name, label);
                if (version >= 2)
                {
                    SaveStringToBytes(pair.Value.Comment, label);
                }
            }

            ByteUtil.AppendIntegerToByteList((uint)allComments.Count, comment);
            foreach (KeyValuePair<int, Comment> pair in allComments)
            {
                ByteUtil.AppendIntegerToByteList((uint)pair.Key, comment);
                SaveStringToBytes(pair.Value.Text, comment);
            }

            // save current Rom full path - "c:\whatever\someRom.sfc"
            var romLocation = ByteUtil.StringToNullTermByteArray(project.AttachedRomFilename);

            var data = new byte[romSettings.Length + romLocation.Length + 8 * size + label.Count + comment.Count];
            romSettings.CopyTo(data, 0);
            for (int i = 0; i < romLocation.Length; i++) data[romSettings.Length + i] = romLocation[i];

            var readOps = new Func<int, byte>[]
            {
                i => (byte)project.Data.GetDataBank(i),
                i => (byte)project.Data.GetDataBank(i),
                i => (byte)project.Data.GetDirectPage(i),
                i => (byte)(project.Data.GetDirectPage(i) >> 8),
                i => (byte)(project.Data.GetXFlag(i) ? 1 : 0),
                i => (byte)(project.Data.GetMFlag(i) ? 1 : 0),
                i => (byte)project.Data.GetFlag(i),
                i => (byte)project.Data.GetArchitecture(i),
                i => (byte)project.Data.GetInOutPoint(i),
            };

            void ReadOperation(int startIdx, int whichOp)
            {
                if (whichOp <= 0 || whichOp > readOps.Length)
                    throw new ArgumentOutOfRangeException(nameof(whichOp));

                var baseidx = startIdx + whichOp * size;
                var op = readOps[whichOp];
                for (var i = 0; i < size; i++)
                {
                    data[baseidx + i] = op(i);
                }
            }

            for (var i = 0; i < readOps.Length; ++i)
            {
                var start = romSettings.Length + romLocation.Length;
                ReadOperation(start, i);
            }
            
            // ???
            label.CopyTo(data, romSettings.Length + romLocation.Length + 8 * size);
            comment.CopyTo(data, romSettings.Length + romLocation.Length + 8 * size + label.Count);
            // ???

            return data;
            #endif
            return null;
        }

        #if ALLOW_OLD_SAVE_FORMATS
        private static void ValidateSaveVersion(int version) {
            if (version < 1 || version > LatestFileFormatVersion) {
                throw new ArgumentException($"Saving: Invalid save version requested for saving: {version}.");
            }
        }
        #endif

        private static void ValidateProjectFileVersion(int version)
        {
            if (version > LatestFileFormatVersion)
            {
                throw new ArgumentException(
                    "This DiztinGUIsh file uses a newer file format! You'll need to download the newest version of DiztinGUIsh to open it.");
            }

            if (version < 0)
            {
                throw new ArgumentException($"Invalid project file version detected: {version}.");
            }
        }

        private void ReadComments(Project project, byte[] bytes, ref int pointer, ByteUtil.AddressConverter converter)
        {
            const int stringsPerEntry = 1;
            pointer += ByteUtil.ReadStringsTable(bytes, pointer, stringsPerEntry, converter, 
                (offset, strings) =>
                {
                    Debug.Assert(strings.Length == 1);
                    project.Data?.AddComment(offset, strings[0], true);
                });
        }

        private void ReadLabels(Project project, byte[] bytes, ref int pointer, ByteUtil.AddressConverter converter, bool readAliasComments)
        {
            var stringsPerEntry = readAliasComments ? 2 : 1;
            pointer += ByteUtil.ReadStringsTable(bytes, pointer, stringsPerEntry, converter,
                (offset, strings) =>
                {
                    Debug.Assert(strings.Length == stringsPerEntry);
                    var label = new Label
                    {
                        Name = strings[0],
                        Comment = strings.ElementAtOrDefault(1)
                    };
                    project.Data.Labels.AddLabel(offset, label, true);
                });
        }
    }
}
