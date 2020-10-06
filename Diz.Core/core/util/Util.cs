using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace DiztinGUIsh
{
    public static class Util
    {
        public enum NumberBase
        {
            Decimal = 3, Hexadecimal = 2, Binary = 8
        }

        public static string NumberToBaseString(int v, NumberBase noBase, int d = -1, bool showPrefix = false)
        {
            var digits = d < 0 ? (int)noBase : d;
            switch (noBase)
            {
                case NumberBase.Decimal:
                    return digits == 0 ? v.ToString("D") : v.ToString("D" + digits);
                case NumberBase.Hexadecimal:
                    if (digits == 0) return v.ToString("X");
                    return (showPrefix ? "$" : "") + v.ToString("X" + digits);
                case NumberBase.Binary:
                    var b = "";
                    var i = 0;
                    while (digits == 0 ? v > 0 : i < digits)
                    {
                        b += (v & 1);
                        v >>= 1;
                        i++;
                    }
                    return (showPrefix ? "%" : "") + b;
            }
            return "";
        }

        public static IEnumerable<string> ReadLines(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0x1000, FileOptions.SequentialScan);
            using var sr = new StreamReader(fs, Encoding.UTF8);

            string line;
            while ((line = sr.ReadLine()) != null)
            {
                yield return line;
            }
        }

        public static long GetFileSizeInBytes(string filename)
        {
            var fi = new FileInfo(filename);
            if (!fi.Exists)
                return -1;

            return fi.Length;
        }

        // https://stackoverflow.com/questions/33119119/unzip-byte-array-in-c-sharp
        public static byte[] TryUnzip(byte[] data)
        {
            try
            {
                using var comp = new MemoryStream(data);
                using var gzip = new GZipStream(comp, CompressionMode.Decompress);
                using var res = new MemoryStream();
                gzip.CopyTo(res);
                return res.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static byte[] TryZip(byte[] data)
        {
            try
            {
                using var comp = new MemoryStream();
                using var gzip = new GZipStream(comp, CompressionMode.Compress);
                gzip.Write(data, 0, data.Length);
                gzip.Close();
                return comp.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string GetEnumDescription(Enum value)
        {
            // example:
            // type = Data.ROMMapMode (the entire enum)
            // value = ExSA1ROM (one particular entry from the enum)
            // description = "SA-1 ROM (FuSoYa's 8MB mapper)",   (contents of [Description] attribute)

            var type = value.GetType();
            var memberInfo = type.GetField(value.ToString());
            var descAttr = (Attribute.GetCustomAttribute(memberInfo, typeof(DescriptionAttribute)) as DescriptionAttribute);
            var name = descAttr?.Description ?? value.ToString();
            return name;
        }


        // take a enum type that has [Description] attributes,
        // return a List with with kvp pairs of enum vs description
        public static List<KeyValuePair<TEnum, string>>
            GetEnumDescriptions<TEnum>() where TEnum : Enum
        {
            var type = typeof(TEnum);
            return Enum.GetValues(type)
                .Cast<TEnum>()
                .Select(value => new
                    KeyValuePair<TEnum, string>(key: value, value: GetEnumDescription(value))
                )
                .OrderBy(item => item.Key)
                .ToList();
        }
    }
}
