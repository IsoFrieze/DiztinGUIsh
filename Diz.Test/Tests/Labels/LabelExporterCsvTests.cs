using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Import;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test.Tests.Labels;

public class LabelExporterCsvTests(ITestOutputHelper output)
{
    private static Dictionary<int, IAnnotationLabel> MakeLabels(
        params (int addr, string name, string comment)[] entries) =>
        entries.ToDictionary(
            x => x.addr,
            x => (IAnnotationLabel)new Label { Name = x.name, Comment = x.comment });

    /// <summary>
    /// Export to a temp .csv, then read it back with the REAL, UNMODIFIED LabelImporterCsv.
    /// This is the acceptance test: the exporter is only correct if the existing importer agrees.
    /// </summary>
    private static (Dictionary<int, IAnnotationLabel> reimported, string csvText, LabelExportResult result)
        RoundTrip(Dictionary<int, IAnnotationLabel> labels)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"diz_labels_{Guid.NewGuid():N}.csv");
        try
        {
            var exporter = new LabelExporterCsv();

            LabelExportResult result;
            using (var writer = new StreamWriter(tempFile))
            {
                result = exporter.WriteLabels(writer, labels);
            }

            var csvText = File.ReadAllText(tempFile);
            var reimported = new LabelImporterCsv().ReadLabelsFromFile(tempFile);
            return (reimported, csvText, result);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    // -------------------------------------------------------------------------------------
    // basic shape
    // -------------------------------------------------------------------------------------

    [Fact]
    public void FormatCsvLine_MatchesTheWinformsFormat()
    {
        // identical to the old OutputCsvLine: 6-digit hex, comma, name, comma, comment.
        LabelExporterCsv.FormatCsvLine(0xC0FFEE, "my_label", "a comment")
            .Should().Be("C0FFEE,my_label,a comment");
    }

    [Fact]
    public void SimpleLabels_RoundTrip()
    {
        var labels = MakeLabels(
            (0x808000, "reset_vector", "entry point"),
            (0x7E0100, "player_hp", ""),
            (0xC00000, "jml_master", "first line of bank C0"));

        var (reimported, _, result) = RoundTrip(labels);

        result.LabelsWritten.Should().Be(3);
        result.Sanitizations.Should().BeEmpty();
        reimported.Should().HaveCount(3);

        foreach (var (addr, original) in labels)
        {
            reimported.Should().ContainKey(addr);
            reimported[addr].Name.Should().Be(original.Name);
            reimported[addr].Comment.Should().Be(original.Comment);
        }
    }

    [Fact]
    public void EmptyNameAndComment_RoundTrip()
    {
        var (reimported, _, _) = RoundTrip(MakeLabels((0x808000, "", "")));
        reimported[0x808000].Name.Should().BeEmpty();
        reimported[0x808000].Comment.Should().BeEmpty();
    }

    [Fact]
    public void NullLabelCollection_IsHandled()
    {
        using var writer = new StringWriter();
        var result = new LabelExporterCsv().WriteLabels(writer, null);
        result.LabelsWritten.Should().Be(0);
        writer.ToString().Should().BeEmpty();
    }

    // -------------------------------------------------------------------------------------
    // escaping edge cases
    // -------------------------------------------------------------------------------------

    /// <summary>
    /// EVIDENCE that a comma in a comment is NOT broken today, and that RFC 4180 quoting would be
    /// actively WRONG here. LabelImporterCsv takes the whole rest of the line as the comment, so an
    /// unquoted comma survives. Had we quoted the field, the quotes would come back as content.
    /// </summary>
    [Fact]
    public void CommaInComment_RoundTripsUnquoted()
    {
        const string comment = "in overworld, seems like a queue of bytes, read for commands";
        var (reimported, csvText, result) = RoundTrip(MakeLabels((0x808000, "lbl", comment)));

        result.Sanitizations.Should().BeEmpty();
        csvText.Should().NotContain("\"", "the importer does not parse quotes; quoting would corrupt data");
        reimported[0x808000].Comment.Should().Be(comment);
    }

    [Fact]
    public void QuoteInComment_RoundTripsLiterally()
    {
        const string comment = "\"battle UI reads from these\" the current controller input";
        var (reimported, _, result) = RoundTrip(MakeLabels((0x808000, "lbl", comment)));

        result.Sanitizations.Should().BeEmpty();
        reimported[0x808000].Comment.Should().Be(comment);
    }

    [Fact]
    public void TabInComment_RoundTrips()
    {
        const string comment = "col1\tcol2\tcol3";
        var (reimported, _, _) = RoundTrip(MakeLabels((0x808000, "lbl", comment)));
        reimported[0x808000].Comment.Should().Be(comment);
    }

    [Fact]
    public void UnicodeInComment_RoundTrips()
    {
        const string comment = "Crono's théme — 音楽 🎵 ünïcode";
        var (reimported, _, result) = RoundTrip(MakeLabels((0x808000, "lbl", comment)));

        result.Sanitizations.Should().BeEmpty();
        reimported[0x808000].Comment.Should().Be(comment);
    }

    /// <summary>
    /// THE REAL PRE-EXISTING BUG. A newline inside a comment splits one label across two physical
    /// lines. The new exporter collapses line breaks to spaces and reports having done so.
    /// </summary>
    [Theory]
    [InlineData("line one\nline two")]
    [InlineData("line one\r\nline two")]
    [InlineData("line one\rline two")]
    public void NewlineInComment_IsSanitizedAndStillRoundTrips(string comment)
    {
        var (reimported, csvText, result) = RoundTrip(MakeLabels((0x808000, "lbl", comment)));

        result.Sanitizations.Should().ContainSingle()
            .Which.Field.Should().Be("Comment");

        csvText.Trim('\r', '\n').Should().NotContainAny("\r", "\n",
            "one label must occupy exactly one physical line");

        reimported.Should().ContainSingle("the stray second line must not become a bogus label");
        reimported[0x808000].Comment.Should().Be("line one line two");
    }

    /// <summary>
    /// Demonstrates the OLD behaviour concretely, so the fix above is provably a fix and not a
    /// theory. This reproduces LabelsView.OutputCsvLine verbatim.
    /// </summary>
    [Fact]
    public void Evidence_OldWinformsExporter_ProducedUnimportableOutput()
    {
        const string comment = "line one\nline two";

        // ---- verbatim port of LabelsView.cs OutputCsvLine (the code being replaced) ----
        static string OldOutputCsvLine(int labelSnesAddress, IReadOnlyLabel label) =>
            $"{Util.ToHexString6(labelSnesAddress)},{label.Name},{label.Comment}";

        var tempFile = Path.Combine(Path.GetTempPath(), $"diz_old_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(tempFile,
                OldOutputCsvLine(0x808000, new Label { Name = "lbl", Comment = comment })
                + Environment.NewLine);

            // the old output is TWO lines for ONE label...
            File.ReadAllLines(tempFile).Should().HaveCount(2);

            // ...and the importer chokes on the orphaned second line, because "line two" is not hex.
            var act = () => new LabelImporterCsv().ReadLabelsFromFile(tempFile);
            act.Should().Throw<Exception>(
                "this is the pre-existing export bug: newline in a comment produces a file the " +
                "importer cannot read back");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void CommaOrWhitespaceInName_IsSanitized()
    {
        var (reimported, _, result) = RoundTrip(MakeLabels(
            (0x808000, "bad,name", ""),
            (0x808010, "trailing_tab\t", "")));

        result.Sanitizations.Should().HaveCount(2);
        result.Sanitizations.Should().OnlyContain(x => x.Field == "Name");

        reimported[0x808000].Name.Should().Be("bad_name");
        reimported[0x808010].Name.Should().Be("trailing_tab");
    }

    // -------------------------------------------------------------------------------------
    // full CT US corpus
    // -------------------------------------------------------------------------------------

    /// <summary>
    /// The headline test: export every label in the real Chrono Trigger US project, re-import it
    /// with the untouched importer, and prove nothing was lost or mangled.
    /// </summary>
    [Fact]
    public void FullCtCorpus_RoundTrips()
    {
        if (!CtLabelCorpus.IsAvailable)
        {
            output.WriteLine("SKIPPED: CT US label corpus not found next to this repo.");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var original = CtLabelCorpus.Load();
        var loadMs = stopwatch.ElapsedMilliseconds;

        original.Count.Should().BeGreaterThan(8000, "the CT US corpus has ~8,400 labels");

        stopwatch.Restart();
        var (reimported, _, result) = RoundTrip(original);
        var roundTripMs = stopwatch.ElapsedMilliseconds;

        output.WriteLine($"corpus: {original.Count} labels | parse {loadMs} ms | " +
                         $"export+reimport {roundTripMs} ms | " +
                         $"sanitized {result.Sanitizations.Count} field(s)");

        foreach (var s in result.Sanitizations)
            output.WriteLine($"  sanitized {s.Field} @ {Util.ToHexString6(s.SnesAddress)}: " +
                             $"{s.Original.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t")}");

        result.LabelsWritten.Should().Be(original.Count);
        reimported.Should().HaveCount(original.Count, "no label may be lost or invented");
        reimported.Keys.Should().BeEquivalentTo(original.Keys);

        // every field that did NOT need sanitizing must come back byte-identical.
        var touched = result.Sanitizations.Select(x => x.SnesAddress).ToHashSet();
        var untouchedCompared = 0;

        foreach (var (addr, orig) in original)
        {
            if (touched.Contains(addr))
                continue;

            reimported[addr].Name.Should().Be(orig.Name, $"name at {Util.ToHexString6(addr)}");
            reimported[addr].Comment.Should().Be(orig.Comment, $"comment at {Util.ToHexString6(addr)}");
            untouchedCompared++;
        }

        output.WriteLine($"exact-match labels compared: {untouchedCompared}");
        untouchedCompared.Should().BeGreaterThan(8000);
    }

    /// <summary>
    /// Export is a fixed point: exporting what we re-imported yields a byte-identical file. This
    /// catches any sanitization that is not idempotent (which would mean data drifting a little
    /// further on every export/import cycle).
    /// </summary>
    [Fact]
    public void FullCtCorpus_ExportIsIdempotent()
    {
        if (!CtLabelCorpus.IsAvailable)
        {
            output.WriteLine("SKIPPED: CT US label corpus not found next to this repo.");
            return;
        }

        var (reimported, firstCsv, _) = RoundTrip(CtLabelCorpus.Load());
        var (_, secondCsv, secondResult) = RoundTrip(reimported);

        secondResult.Sanitizations.Should().BeEmpty(
            "a file that already round-tripped needs no further sanitizing");
        secondCsv.Should().Be(firstCsv);
    }

    /// <summary>
    /// Documents what the corpus actually contains, and pins the two known problem classes so a
    /// future data change is noticed rather than silently absorbed.
    /// </summary>
    [Fact]
    public void FullCtCorpus_SanitizationsAreOnlyTheKnownProblems()
    {
        if (!CtLabelCorpus.IsAvailable)
        {
            output.WriteLine("SKIPPED: CT US label corpus not found next to this repo.");
            return;
        }

        var (_, _, result) = RoundTrip(CtLabelCorpus.Load());

        // comments needing repair contain a line break; names needing repair contain a TAB.
        // NOTE: use NotContain(<negated predicate>) rather than OnlyContain, because OnlyContain
        // FAILS on an empty collection. The name sanitizations legitimately went to zero when
        // chronotrigger-disassembly commit 9c299f8 ("labels: strip trailing tab from two overworld
        // pointer label names") cleaned the corpus. A clean corpus must pass, not fail.
        result.Sanitizations
            .Where(x => x.Field == "Comment")
            .Should().NotContain(x => !x.Original.Contains('\n') && !x.Original.Contains('\r'));

        result.Sanitizations
            .Where(x => x.Field == "Name")
            .Should().NotContain(x => x.Original.Trim() == x.Original && !x.Original.Contains(','));

        output.WriteLine($"comment fixes: {result.Sanitizations.Count(x => x.Field == "Comment")}");
        output.WriteLine($"name fixes:    {result.Sanitizations.Count(x => x.Field == "Name")}");
    }
}
