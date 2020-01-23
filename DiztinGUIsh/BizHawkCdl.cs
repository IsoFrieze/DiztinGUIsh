using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiztinGUIsh
{
    public class BizHawkCdl : Dictionary<string, IList<BizHawkCdl.Flag>>
    {
        [Flags]
        public enum Flag : byte
        {
            None = 0x00,
            ExecFirst = 0x01,
            ExecOperand = 0x02,
            CPUData = 0x04,
            DMAData = 0x08,
            CPUXFlag = 0x10,
            CPUMFlag = 0x20,
            BRR = 0x80
        }

        public static BizHawkCdl LoadFromFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open))
            {
                return LoadFromStream(fs);
            }
        }

        public static BizHawkCdl LoadFromStream(Stream input)
        {
            var br = new BinaryReader(input);

            string id = br.ReadString();
            string subType;
            if (id == "BIZHAWK-CDL-1")
            {
                subType = "PCE";
            }
            else if (id == "BIZHAWK-CDL-2")
            {
                subType = br.ReadString().TrimEnd(' ');
            }
            else
            {
                throw new InvalidDataException("File is not a BizHawk CDL file.");
            }

            if (subType != "SNES")
            {
                throw new InvalidDataException("The CDL file is not for SNES.");
            }

            var cdl = new BizHawkCdl();
            int count = br.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string key = br.ReadString();
                int len = br.ReadInt32();
                var data = br.ReadBytes(len).Select(b => (Flag)b).ToArray();
                cdl[key] = data;
            }

            return cdl;
        }
    }
}
