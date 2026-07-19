using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Diz.Spike.Avalonia;

public sealed class LabelRow
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Comment { get; init; } = "";

    // Convenience for the simplest possible DataTemplate.
    public string Display => $"{Key,-10} {Name}";
}

/// <summary>
/// Deliberately crude parser for the Diz label save format. This is a spike:
/// the point is VOLUME of rows, not parse correctness.
///
/// Observed format (00004_save_Labels.txt, CT US):
///   &lt;sys:Item Key="88"&gt;
///     &lt;Value exs:type="ns1:Label" Name="foo" Comment="bar" /&gt;
///   &lt;/sys:Item&gt;
/// The file is an XML *fragment* with undeclared namespace prefixes, so a real
/// XML parser rejects it outright. Regex is correct-enough here.
/// </summary>
public static class LabelLoader
{
    public const string DefaultCorpusPath =
        @"C:\projects\romhax\wt\new_ui\chronotrigger-disassembly\Chrono Trigger US\00004_save_Labels.txt";

    private static readonly Regex KeyRx =
        new(@"<sys:Item\s+Key=""([^""]*)""", RegexOptions.Compiled);

    private static readonly Regex ValueRx =
        new(@"Name=""([^""]*)""\s+Comment=""([^""]*)""", RegexOptions.Compiled);

    public static List<LabelRow> Load(string path = DefaultCorpusPath)
    {
        var sw = Stopwatch.StartNew();
        var rows = new List<LabelRow>(16384);

        if (!File.Exists(path))
        {
            SpikeLog.Write($"CORPUS MISSING: {path} -- falling back to synthetic rows");
            for (var i = 0; i < 8400; i++)
                rows.Add(new LabelRow { Key = i.ToString(), Name = $"synthetic_label_{i}", Comment = "" });
            return rows;
        }

        string pendingKey = null;
        foreach (var line in File.ReadLines(path))
        {
            var k = KeyRx.Match(line);
            if (k.Success)
            {
                pendingKey = k.Groups[1].Value;
                continue;
            }

            var v = ValueRx.Match(line);
            if (v.Success && pendingKey != null)
            {
                rows.Add(new LabelRow
                {
                    Key = pendingKey,
                    Name = v.Groups[1].Value,
                    Comment = v.Groups[2].Value,
                });
                pendingKey = null;
            }
        }

        sw.Stop();
        SpikeLog.Write($"LabelLoader: parsed {rows.Count} rows from {new FileInfo(path).Length:N0} bytes in {sw.ElapsedMilliseconds} ms");
        return rows;
    }
}
