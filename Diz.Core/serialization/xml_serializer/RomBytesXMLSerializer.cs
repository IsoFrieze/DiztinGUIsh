﻿// define to do some extra checking as we save the data out
// #define EXTRA_DEBUG_CHECKS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Diz.Core.model;
using ExtendedXmlSerializer.ContentModel;
using ExtendedXmlSerializer.ContentModel.Format;

// everything else in the savefiles is straight up normal XML, but,
// the amount of bytes for the ROM metadata can be huge.
// by using a custom serializer for this one section, we can cut a 4MB rom savefile down from ~45MB to ~1.5MB
//
// This uses some hacky compression methods designed to preserve:
// 1) newlines
// 2) slight human readability for merging, so projects can be collaborated on in source control
// 3) using pattern substitution tables that won't change from PC to PC.
//
// The easiest thing would be use zlib (and we can use it with the output of the entire XML if we want)
// but, for projects with multiple collaborators using diztinGUIsh, mergability in text/git/editors/etc
// is a must.  We aim for a tradeoff between decent compression and some small semblance of human readability.
//
// It's not.. super-pretty code, but it compresses well.
namespace Diz.Core.serialization.xml_serializer
{
    sealed class RomBytesSerializer : ISerializer<RomBytes>
    {
        // let the outer XML class do the heavy lifting on versioning.
        // but, let's add one here just because this specific class is complex.
        private const int CurrentDataFormatVersion = 200;

        public static RomBytesSerializer Default { get; } = new();

        // These settings control some (ad-hoc / hacky) compression for output text.
        // The goal is *purely* to reduce the size of the final XML file, but do so in a way that is still [somewhat] mergeable
        // for team collaboration using tools like 'git merge' etc.
        //
        // These settings ONLY affect data that's being serialized (Save project) and whether we attempt to compress it a bit more.
        // For data that's being deserialized (during Load Project), we need to always be able to deal with these encodings.
        //
        // For the most mergeable version (but, at the cost of slightly larger filesize), leave both OFF.
        //
        // CompressGroupBlock: Find output lines that are identical, and use run-length encoding to mark how many times a line should be repeated.
        // Diz projects tend to be FULL of lines like this for huge sections of content/graphics/music/etc.
        // This is a huge win for filesize, though, it makes merging harder.
        public const bool CompressGroupBlock = true;
        //
        // CompressUsingTable1: Take the output lines, and run some substitutions for the most common patterns we see in Diz files
        // (as of a few analyzed in 2021). This is like an incredibly crappy gzip-style encoding, but, still outputs readable text that 
        // preserves line breaks (so it can still be merged).
        // This won't change the # of output lines (just their content), so it's pretty merge-friendly. Humans won't be able to read the lines well though,
        // it'll just look like "base64"-ish gibberish.
        // Still, a pretty nice win and solid tradeoff of filesize and human text merging friendliness.
        public const bool CompressUsingTable1 = true;
        
        public int numTasksToUse = 5; // seems like the sweet spot

        public RomBytes Get(IFormatReader parameter)
        {
            var allLines = ReadMainDataRaw(parameter.Content());
            var romBytes = DecodeAllBytes(allLines);
            return FinishRead(romBytes);
        }

        private RomByte[] DecodeAllBytes(List<string> allLines)
        {
            if (numTasksToUse == 1)
                return DecodeRomBytes(allLines, 0, allLines.Count);

            var tasks = new List<Task<RomByte[]>>(numTasksToUse);

            var nextIndex = 0;
            var workListCount = allLines.Count / numTasksToUse;

            for (var t = 0; t < numTasksToUse; ++t)
            {
                if (t == numTasksToUse - 1)
                    workListCount = allLines.Count - nextIndex;

                tasks.Add(CreateDecodeRomBytesTask(allLines, nextIndex, workListCount));

                nextIndex += workListCount;
            }

            var continuation = Task.WhenAll(tasks);
            continuation.Wait();
            return continuation.Result.SelectMany(i => i).ToArray();
        }

        private static Task<RomByte[]> CreateDecodeRomBytesTask(List<string> allLines, int nextIndex, int workListCount)
        {
            // ReSharper disable once AccessToStaticMemberViaDerivedType
            return Task<RomByte[]>.Run(() => DecodeRomBytes(allLines, nextIndex, workListCount));
        }

        private static RomByte[] DecodeRomBytes(IReadOnlyList<string> allLines, int startIndex, int count)
        {
            // perf: allocate all at once, don't use List.Add() one at a time
            var romBytes = new RomByte[count];
            var romByteEncoding = new RomByteEncoding();
            var i = 0;

            try
            {
                while (i < count)
                {
                    var line = allLines[startIndex + i];
                    romBytes[i] = romByteEncoding.DecodeRomByte(line);
                    ++i;
                }
            }
            catch (Exception ex)
            {
                ex.Data.Add("ParseLineNum", "Near line# " + (startIndex + i));
                throw;
            }

            return romBytes;
        }

        private static RomBytes FinishRead(RomByte[] romBytes)
        {
            var romBytesOut = new RomBytes();
            romBytesOut.SetFrom(romBytes);
            return romBytesOut;
        }

        private static List<string> ReadMainDataRaw(string allLines)
        {
            var (lines, options) = ReadHeader(allLines);

            CheckForCompatibleVersion(options);

            // always apply options in same order here and in saving function
            if (options.Exists(s => s == "compress_table_1"))
                SubstitutionCompression.DecodeCompression_Table1(ref lines);

            if (options.Exists(s => s == "compress_groupblocks"))
                RepeaterCompression.Decompress(ref lines);
            return lines;
        }

        private static (List<string> lines, List<string> options) ReadHeader(string allLines)
        {
            var lines = allLines.Split(new char[] {'\n'}, 3).ToList();
            var options = lines[1].Split(new char[] {','}).ToList();
            lines = lines[2].Split(new char[] {'\n'}).ToList();
            if (lines[lines.Count - 1] == "")
                lines.RemoveAt(lines.Count - 1);
            return (lines, options);
        }

        private static void CheckForCompatibleVersion(IEnumerable<string> options)
        {
            try
            {
                var versionOption = options.SingleOrDefault(s => s.Contains("version:"));

                if (versionOption == null)
                {
                    throw new InvalidDataException(
                        $"Exactly 1 'version' tag must be in options, unable to continue");
                }

                var split = versionOption.Split(':');
                Debug.Assert(split.Length == 2);
                if (!int.TryParse(split[1], out var versionNum))
                    throw new InvalidDataException(
                        $"Couldn't parse version # from version tag");

                if (versionNum > CurrentDataFormatVersion)
                    throw new InvalidDataException(
                        $"Newer file format detected: {versionNum}. This version of distinguish only supports data table formats up to {CurrentDataFormatVersion}.");

                // In the future, we can add migrations here for older version. For now, just reject it.
                if (versionNum < CurrentDataFormatVersion)
                    throw new InvalidDataException(
                        $"Newer file format detected: {versionNum}. This version of distinguish only supports data table formats up to {CurrentDataFormatVersion}.");
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Data table loader: Version error: {ex.Message}");
            }
        }

        public void Write(IFormatWriter writer, RomBytes instance)
        {
            var options = new List<string>
            {
                $"version:{CurrentDataFormatVersion}",
            };

            var romByteEncoding = new RomByteEncoding();

            var lines = new List<string>();
            foreach (var rb in instance)
            {
                var encodedTxt = romByteEncoding.EncodeByte(rb);
                lines.Add(encodedTxt);

                // debug check, optional:
#if EXTRA_DEBUG_CHECKS
                var decoded = romByteEncoding.DecodeRomByte(encodedTxt);
                Debug.Assert(decoded.EqualsButNoRomByte(rb));
#endif
            }

            if (CompressGroupBlock)
            {
                options.Add("compress_groupblocks");
                RepeaterCompression.Compress(ref lines);
            }

            if (CompressUsingTable1)
            {
                options.Add("compress_table_1");
                SubstitutionCompression.EncodeCompression_Table1(ref lines);
            }

            writer.Content($"\n{string.Join(",", options)}\n");

            foreach (var line in lines)
            {
                writer.Content(line + "\n");
            }
        }
    }
}