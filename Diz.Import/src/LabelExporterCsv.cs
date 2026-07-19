using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Import;

/// <summary>
/// One field that had to be altered to make a line round-trippable.
/// </summary>
/// <param name="SnesAddress">address of the label the change was made to</param>
/// <param name="Field">"Name" or "Comment"</param>
/// <param name="Original">value as stored in the project</param>
/// <param name="Written">value actually written to the .csv</param>
public readonly record struct LabelExportSanitization(
    int SnesAddress, string Field, string Original, string Written);

/// <summary>
/// Outcome of a CSV export.
/// </summary>
public sealed class LabelExportResult
{
    public int LabelsWritten { get; init; }

    /// <summary>
    /// Fields whose content could not survive a round-trip and were rewritten. Empty in the
    /// overwhelmingly common case. Callers may surface these to the user; nothing is silently lost
    /// without being listed here.
    /// </summary>
    public IReadOnlyList<LabelExportSanitization> Sanitizations { get; init; } =
        Array.Empty<LabelExportSanitization>();
}

/// <summary>
/// Writes labels out as CSV, in the exact dialect <see cref="LabelImporterCsv"/> reads back in.
///
/// WHY THIS EXISTS: label CSV *import* already lived here in Diz.Import, but *export* was
/// hand-rolled inside a WinForms usercontrol (LabelsView.cs WriteLabelsToCsv / OutputCsvLine).
/// That asymmetry is the bug this class fixes. Logic ported from there; behaviour differences are
/// listed under "DIFFERENCES" below.
///
/// ---------------------------------------------------------------------------------------------
/// DIALECT WARNING -- this is deliberately NOT RFC 4180, and must not be "fixed" into RFC 4180.
/// ---------------------------------------------------------------------------------------------
/// LabelImporterCsv does not parse quotes at all. It calls Util.SplitOnFirstComma twice and takes
/// the ENTIRE REST OF THE LINE as the comment. Consequences, all verified against that parser:
///
///   * a comma in a comment is already safe      -- it lands in "rest of line" and round-trips.
///   * a double-quote in a comment is already safe -- it is never treated as a delimiter.
///   * therefore, adding RFC 4180 quoting here would BREAK the round-trip: the importer would
///     read the quote characters back as literal content.
///
/// So the exporter matches the importer instead of the standard. The only characters that
/// genuinely cannot survive are the ones that end a line.
///
/// ---------------------------------------------------------------------------------------------
/// DIFFERENCES from the WinForms OutputCsvLine (all bug fixes; see LabelExporterCsvTests):
/// ---------------------------------------------------------------------------------------------
///  1. CR/LF inside a Name or Comment is replaced with a single space. The old code wrote them
///     raw, which split one label across two physical lines; on re-import the second line was
///     parsed as a bogus label (and typically threw, since its "address" is not hex).
///     This IS reachable in real data: 13 comments in the CT US corpus contain newlines.
///  2. A Name containing a comma, or leading/trailing whitespace, is sanitized. The importer
///     splits on the first comma and Trim()s the name, so such a name could not round-trip.
///     (LabelNameValidator rejects these characters, so this is a belt-and-braces guard for
///     data that predates validation -- e.g. 2 CT US corpus names have a trailing TAB.)
///  3. Anything altered is reported in <see cref="LabelExportResult.Sanitizations"/> rather than
///     being dropped on the floor.
///
/// NOTE: there is no IFilesystemService overload because that interface only exposes
/// DirectoryExists/CreateDirectory -- it has no file-writing member. The TextWriter overload is
/// the testable seam instead.
/// </summary>
public class LabelExporterCsv
{
    /// <summary>
    /// Characters that would terminate the record. These are the only ones that actually break
    /// the importer's line-based parse.
    /// </summary>
    private static readonly char[] LineBreakChars = ['\r', '\n'];

    /// <summary>
    /// Format one label as it will appear in the file, WITHOUT the trailing newline.
    /// Pure function, exposed for testing.
    /// </summary>
    public static string FormatCsvLine(int labelSnesAddress, string name, string comment) =>
        $"{Util.ToHexString6(labelSnesAddress)},{name},{comment}";

    /// <summary>
    /// Write every label to <paramref name="textWriter"/>, one per line.
    /// </summary>
    public LabelExportResult WriteLabels(
        TextWriter textWriter,
        IEnumerable<KeyValuePair<int, IAnnotationLabel>>? labels)
    {
        ArgumentNullException.ThrowIfNull(textWriter);

        if (labels == null)
            return new LabelExportResult { LabelsWritten = 0 };

        var sanitizations = new List<LabelExportSanitization>();
        var count = 0;

        foreach (var (snesAddress, label) in labels)
        {
            if (label == null)
                continue;

            var name = SanitizeName(snesAddress, label.Name ?? "", sanitizations);
            var comment = SanitizeComment(snesAddress, label.Comment ?? "", sanitizations);

            textWriter.WriteLine(FormatCsvLine(snesAddress, name, comment));
            count++;
        }

        return new LabelExportResult
        {
            LabelsWritten = count,
            Sanitizations = sanitizations,
        };
    }

    /// <summary>
    /// Convenience: write every label in <paramref name="labelProvider"/> to a file.
    /// </summary>
    public LabelExportResult ExportLabelsToFile(string filename, IReadOnlyLabelProvider labelProvider)
    {
        ArgumentNullException.ThrowIfNull(labelProvider);
        using var streamWriter = new StreamWriter(filename);
        return WriteLabels(streamWriter, labelProvider.Labels);
    }

    /// <summary>
    /// The importer splits the name on the first comma and then Trim()s it, so a name containing a
    /// comma or edge whitespace cannot come back intact. Also strips line breaks.
    /// </summary>
    private static string SanitizeName(
        int snesAddress, string original, ICollection<LabelExportSanitization> sanitizations)
    {
        var written = ReplaceLineBreaks(original);
        written = written.Replace(',', '_');
        written = written.Trim();

        if (written != original)
            sanitizations.Add(new LabelExportSanitization(snesAddress, "Name", original, written));

        return written;
    }

    /// <summary>
    /// Comments may contain commas and quotes freely (see DIALECT WARNING). Only line breaks
    /// need removing.
    /// </summary>
    private static string SanitizeComment(
        int snesAddress, string original, ICollection<LabelExportSanitization> sanitizations)
    {
        var written = ReplaceLineBreaks(original);

        if (written != original)
            sanitizations.Add(new LabelExportSanitization(snesAddress, "Comment", original, written));

        return written;
    }

    /// <summary>
    /// Collapse CRLF / CR / LF to a single space, so one label always occupies exactly one line.
    /// </summary>
    private static string ReplaceLineBreaks(string input)
    {
        if (input.IndexOfAny(LineBreakChars) == -1)
            return input;

        return input
            .Replace("\r\n", " ")
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}
