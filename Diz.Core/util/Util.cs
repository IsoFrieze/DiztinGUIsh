using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Diz.Core.model;
using IX.StandardExtensions;

namespace Diz.Core.util
{
    public static class Util
    {
        // https://stackoverflow.com/questions/703281/getting-path-relative-to-the-current-working-directory/703290#703290
        public static string GetRelativePath(string fileSpec, string folder)
        {
            var pathUri = new Uri(fileSpec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            var folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
        
        public static string TryGetRelativePath(string fileSpec, string folder = null)
        {
            if (string.IsNullOrEmpty(folder))
                return fileSpec;
            
            try
            {
                return GetRelativePath(fileSpec, folder);
            }
            catch (Exception)
            {
                return fileSpec;
            }
        }
        
        public static string GetDirNameOrEmpty(string path) => 
            string.IsNullOrEmpty(path) ? "" : Path.GetDirectoryName(path);

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
        
        public static string ToHexString6(int i)
        {
            return NumberToBaseString(i, NumberBase.Hexadecimal, 6);
        }
        
        public static int ParseHexOrBase10String(string data)
        {
            var hex = false;
            var hexDigitStartIndex = 0;

            if (data.Length > 1)
            {
                hex = data[0] == '$';
                hexDigitStartIndex = 1;
            }
            else if (data.Length > 2)
            {
                hexDigitStartIndex = 2;
                hex =
                    data[0] == '0' && data[1] == 'x' ||
                    data[0] == '#' && data[1] == '$';
            }

            if (hex)
                return (int)ByteUtil.ByteParseHex(data, hexDigitStartIndex, data.Length - hexDigitStartIndex);

            return int.Parse(data);
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

        public static byte[] ReadNext(Stream stream, int count, out int bytesRead)
        {
            var buffer = new byte[count];
            bytesRead = ReadNext(stream, buffer, count);
            return buffer;
        }

        public static int ReadNext(Stream stream, byte[] buffer, int count)
        {
            // not in love with this.
            return ReadNext(stream, buffer, count, stream is NetworkStream);
        }

        public static int ReadNext(Stream stream, byte[] buffer, int count, bool continueOnZeroBytesRead=false)
        {
            var offset = 0;

            while (count > 0)
            {
                var bytesRead = stream.Read(buffer, offset, count);
                
                count -= bytesRead;
                offset += bytesRead;

                if (bytesRead == 0 && !continueOnZeroBytesRead)
                    break;
            }

            if (count > 0)
                throw new EndOfStreamException();

            return offset;
        }

        public static string GetEnumDescription(Enum value)
        {
            // example:
            // type = Data.ROMMapMode (the entire enum)
            // value = ExSA1ROM (one particular entry from the enum)
            // description = "SA-1 ROM (FuSoYa's 8MB mapper)",   (contents of [Description] attribute)

            return GetEnumAttribute(
                value, 
                (DescriptionAttribute d) => d?.Description
                ) ?? value.ToString();
        }

        public static TResult GetEnumAttribute<TAttribute, TResult>(object value, Func<TAttribute, TResult> getValueFn) where TAttribute : Attribute
        {
            return GetFieldAttribute(getValueFn, value.GetType(), value.ToString());
        }

        public static TResult GetFieldAttribute<TAttribute, TResult>(Func<TAttribute, TResult> getValueFn, Type type, string memberName)
            where TAttribute : Attribute
        {
            var memberInfo = type.GetField(memberName);
            if (memberInfo == null)
                return default;
            
            var attr = (TAttribute) Attribute.GetCustomAttribute(memberInfo, typeof(TAttribute));
            return getValueFn(attr);
        }
        
        public static TResult GetPropertyAttribute<TAttribute, TResult>(Func<TAttribute, TResult> getValueFn, Type type, string propertyName)
            where TAttribute : Attribute
        {
            var property = type.GetProperty(propertyName);
            if (property == null)
                return default;
            
            var attr = (TAttribute) Attribute.GetCustomAttribute(property, typeof(TAttribute));
            return getValueFn(attr);
        }

        // take a enum type that has [Description] attributes,
        // return a List with with kvp pairs of enum vs description
        public static List<KeyValuePair<TEnum, string>> GetEnumDescriptions<TEnum>() where TEnum : Enum
        {
            return GetEnumInfo<TEnum, string>((value) => GetEnumDescription(value));
        }

        // perf: might be a little slow, caution when in tight loops
        public static List<KeyValuePair<TEnum, KnownColor>> GetEnumColorDescriptions<TEnum>() where TEnum : Enum
        {
            return GetEnumInfo<TEnum, KnownColor>((value) => GetEnumColor(value));
        }

        private static KnownColor GetEnumColor(Enum value)
        {
            return GetEnumAttribute(
                value,
                (ColorDescriptionAttribute d) => d?.Color
            ) ?? KnownColor.Black;
        }

        public static List<KeyValuePair<TEnum, TType>> GetEnumInfo<TEnum, TType>(Func<TEnum, TType> getValue) where TEnum : Enum
        {
            var type = typeof(TEnum);
            return Enum.GetValues(type)
                .Cast<TEnum>()
                .Select(value => new
                    KeyValuePair<TEnum, TType>(key: value, value: getValue(value))
                )
                .OrderBy(item => item.Key)
                .ToList();
        }

        // sadly, this entire conversion is a bit slow so, cache it as we look it up
        private static readonly Dictionary<FlagType, Color> CachedRomFlagColors =
            new Dictionary<FlagType, Color>();

        public static Color GetColorFromFlag(FlagType romFlag)
        {
            if (CachedRomFlagColors.TryGetValue(romFlag, out var color))
                return color;

            color = Color.FromKnownColor(GetEnumColor(romFlag)); // slow (comparatively)
            CachedRomFlagColors[romFlag] = color;

            return color;
        }

        public static void OpenExternalProcess(string args)
        {
            var info = new ProcessStartInfo(args) { UseShellExecute = true };
            Process.Start(info);
        }

        // clamp index so index >= 0 and index < size
        // for arrays
        public static int ClampIndex(int index, int size) => ClampIndex(index, 0, size - 1);
        
        // clamp index so index >= minIndex and index <= maxIndex
        public static int ClampIndex(int index, int minIndex, int maxIndex)
        {
            if (minIndex < 0 || minIndex > maxIndex)
                throw new ArgumentNotInRangeException("ClampIndex params not in range");
            
            return index > maxIndex
                ? maxIndex
                : index < minIndex
                    ? 0
                    : index;
        }

        public static void SplitOnFirstComma(string instr, out string firstPart, out string remainder)
        {
            if (!instr.Contains(","))
            {
                firstPart = instr;
                remainder = "";
                return;
            }

            firstPart = instr.Substring(0, instr.IndexOf(','));
            remainder = instr.Substring(instr.IndexOf(',') + 1);
        }
    }
}
