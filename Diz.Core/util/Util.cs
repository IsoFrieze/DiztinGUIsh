using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Diz.Core.export;
using Diz.Core.model;
using JetBrains.Annotations;

namespace Diz.Core.util
{
    public static class Util
    {
        public static void RemoveAll<TItem>(this ICollection<TItem> collection, Predicate<TItem> match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            // make a copy to avoid removing items from the list we're enumerating
            collection.Where(item => match(item))
                .ToList()
                .ForEach(item => collection.Remove(item));
        }
        
        public static void AddRange<TItem>(this ICollection<TItem> collection, IEnumerable<TItem> newItems)
        {
            foreach (var newItem in newItems) 
                collection.Add(newItem);
        }
        
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
            return DoReadNext(stream, buffer, count, stream is NetworkStream);
        }

        private static int DoReadNext(Stream stream, byte[] buffer, int count, bool continueOnZeroBytesRead=false)
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
        private static readonly Dictionary<FlagType, Color> CachedRomFlagColors = new();

        public static Color GetColorFromFlag(FlagType romFlag)
        {
            if (CachedRomFlagColors.TryGetValue(romFlag, out var color))
                return color;

            color = Color.FromKnownColor(GetEnumColor(romFlag)); // slow (comparatively)
            CachedRomFlagColors[romFlag] = color;

            return color;
        }
        
        public static (string stdout, string stderr) RunCommandGetOutput(string cmdExePath, string args)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = cmdExePath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            
            var output = process.StandardOutput.ReadToEnd();
            Console.WriteLine(output);
            
            var err = process.StandardError.ReadToEnd();
            Console.WriteLine(err);
            
            process.WaitForExit();

            return (output, err);
        }

        public static void OpenExternalProcess(string args)
        {
            var info = new ProcessStartInfo(args) { UseShellExecute = true };
            Process.Start(info);
        }

        // clamp index so index >= 0 and index < size
        // for arrays
        public static int ClampIndex(int index, int size) => Clamp(index, 0, size - 1);
        
        // returns i if i >= min and index <= max, otherwise returns min or max
        public static int Clamp(int i, int min, int max)
        {
            if (min < 0 || min > max)
                throw new IndexOutOfRangeException("ClampIndex params not in range");
            
            return i > max 
                ? max
                : i < min
                    ? min
                    : i;
        }

        public static bool IsBetween(int i, int max) => i >= 0 && i <= max;
        public static bool IsBetween(int i, int min, int max) => i >= min && i <= max;

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

        public static string LeftAlign(int length, string str) => string.Format(GetLeftAlignFormatStr(length), str);

        public static string GetLeftAlignFormatStr(int length) => $"{{0,{-length}}}";
        
        public static string ReadManifestData(Assembly assembly, string resourceName)
        {
            resourceName = resourceName.Replace("/", ".");
            using var stream = assembly.GetManifestResourceStream(resourceName);
            
            if (stream == null)
                throw new InvalidOperationException("Could not load manifest resource stream.");

            return new StreamReader(stream).ReadToEnd();
        }

        // decent helper check before doing a deep compare on two lists.
        // considers them equal if any are true:
        // - both lists are null
        // - if one list is non-null, and the other contains zero elements
        #if DIZ_3_BRANCH // this isn't needed yet but will be part of v3.0.
        public static bool BothListsNullOrContainNoItems<T>(ICollection<T> c1, ICollection<T> c2)
        {
            return c1 switch
            {
                null when c2 == null => true,
                null => c2.Count == 0,
                not null when c2 == null => c1.Count == 0,
                _ => false
            };
        }
        
        public static bool CollectionsBothEmptyOrEqual<T>(ICollection<T> c1, ICollection<T> c2)
        {
            if (Util.BothListsNullOrContainNoItems(c1, c2)) 
                return true;

            return c1?.SequenceEqual(c2) ?? false;
        } 
        #endif
    }
    
    
    // makes it a little easier to deal with INotifyPropertyChanged in derived classes
    public interface INotifyPropertyChangedExt : INotifyPropertyChanged
    {
        // would be great if this didn't have to be public. :shrug:
        void OnPropertyChanged(string propertyName);
    }
    
    public static class NotifyPropertyChangedExtensions
    {
        /// <summary>
        /// Set a field, and if changed, dispatch any events associated with it
        /// </summary>
        /// <returns>true if we set property to a new value and dispatched events</returns>
        public static bool SetField<T>(this INotifyPropertyChanged sender, PropertyChangedEventHandler handler, ref T field, T value, bool compareRefOnly = false, [CallerMemberName] string propertyName = null)
        {
            if (FieldIsEqual(field, value, compareRefOnly)) 
                return false;
            
            field = value;
            
            handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        
        /// <summary>
        /// Set a field, and if changed, dispatch any events associated with it
        /// </summary>
        /// <returns>true if we set property to a new value and dispatched events</returns>
        public static bool SetField<T>(this INotifyPropertyChangedExt sender, ref T field, T value, bool compareRefOnly = false, [CallerMemberName] string propertyName = null)
        {
            if (FieldIsEqual(field, value, compareRefOnly)) 
                return false;
            
            field = value;
            
            sender.OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Test if one field is equal to another
        /// </summary>
        /// <returns>true if we equal</returns>
        public static bool FieldIsEqual<T>(T field, T value, bool compareRefOnly = false)
        {
            if (compareRefOnly)
            {
                if (ReferenceEquals(field, value))
                    return true;
            }
            else if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return true;
            }

            return false;
        }
    }

    public static class ContentUtils
    {
        public static Dictionary<int, Label> ReadLabelsFromCsv(string importFilename, out int errLine)
        {
            var newValues = new Dictionary<int, Label>();
            var lines = Util.ReadLines(importFilename).ToArray();

            var validLabelChars = new Regex(@"^([a-zA-Z0-9_\-]*)$");

            errLine = 0;

            // NOTE: this is kind of a risky way to parse CSV files, won't deal with weirdness in the comments
            // section. replace with something better
            for (var i = 0; i < lines.Length; i++)
            {
                var label = new Label();

                errLine = i + 1;

                Util.SplitOnFirstComma(lines[i], out var labelAddress, out var remainder);
                Util.SplitOnFirstComma(remainder, out var labelName, out var labelComment);

                label.Name = labelName.Trim();
                label.Comment = labelComment;

                if (!validLabelChars.Match(label.Name).Success)
                    throw new InvalidDataException("invalid label name: " + label.Name);

                newValues.Add(int.Parse(labelAddress, NumberStyles.HexNumber, null), label);
            }

            errLine = -1;
            return newValues;
        }
        
        public static void ImportLabelsFromCsv(this ILabelProvider labelProvider, string importFilename, bool replaceAll, ref int errLine)
        {
            var labelsFromCsv = ReadLabelsFromCsv(importFilename, out errLine);
            
            if (replaceAll)
                labelProvider.DeleteAllLabels();
            
            foreach (var (key, value) in labelsFromCsv)
            {
                labelProvider.AddLabel(key, value, true);
            }
        }

        public static object SingleOrDefaultOfType<T>(this IEnumerable<T> enumerable, Type desiredType)
        {
            return enumerable.SingleOrDefault(item => item.GetType() == desiredType);
        }
        
        [CanBeNull]
        public static TDesired SingleOrDefaultOfType<TDesired, T>(this IEnumerable<T> enumerable)
        {
            return enumerable.OfType<TDesired>().SingleOrDefault();
        }
    }
}
