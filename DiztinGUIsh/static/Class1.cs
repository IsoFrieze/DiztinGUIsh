using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using DiztinGUIsh;


class TextLoader
{
    void Save(string filename)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(Data));
        TextWriter writer = new StreamWriter(filename);

        serializer.Serialize(writer, Data.Inst);
        writer.Close();

        // int size = Data.Inst.GetROMSize();

        // byte[] romSettings = new byte[31];
        //romSettings[0] = (byte)Data.Inst.GetROMMapMode();
        //romSettings[1] = (byte)Data.Inst.GetROMSpeed();



        /*(Util.IntegerIntoByteArray(size, romSettings, 2);
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

        return data;*/
    }
}
