using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Diz.Core.Interfaces;
using Diz.Core.model;

namespace Diz.Test.Tests.Labels;

/// <summary>
/// Loads the real Chrono Trigger (US) label corpus -- ~8,400 hand-authored labels -- so the
/// validator and the CSV exporter are exercised against real data rather than only invented cases.
///
/// The corpus lives in the SIBLING chronotrigger-disassembly worktree, not in this repo, so it may
/// legitimately be absent (fresh clone / CI). Tests must call <see cref="IsAvailable"/> and skip
/// rather than fail when it is missing.
///
/// The file is the Diz project's saved label blob. It is XML-SHAPED but is NOT a well-formed XML
/// document: it uses the prefixes sys:, exs: and ns1: without ever declaring those namespaces, so
/// XDocument.Parse rejects it. Hence the deliberate hand-parse below.
/// </summary>
public static class CtLabelCorpus
{
    private const string RelativeCorpusPath =
        @"chronotrigger-disassembly\Chrono Trigger US\00004_save_Labels.txt";

    private static readonly Lazy<string?> ResolvedPath = new(FindCorpusFile);

    public static bool IsAvailable => ResolvedPath.Value != null;

    public static string Path =>
        ResolvedPath.Value ?? throw new InvalidOperationException("CT label corpus not available");

    /// <summary>
    /// Walk up from the test assembly looking for a directory that contains the corpus. This finds
    /// it whether the worktree is laid out as wt/new_ui/{DiztinGUIsh,chronotrigger-disassembly} or
    /// somewhere else, without hardcoding one machine's absolute path.
    /// </summary>
    /// <summary>
    /// Explicit override, so the corpus tests can be run from a build output that does not sit
    /// next to the chronotrigger-disassembly worktree (e.g. a scratch/CI checkout).
    /// </summary>
    public const string PathEnvironmentVariable = "DIZ_CT_LABEL_CORPUS";

    private static string? FindCorpusFile()
    {
        var overridePath = Environment.GetEnvironmentVariable(PathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return overridePath;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, RelativeCorpusPath);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }

    // one <sys:Item Key="NNN"> ... <Value exs:type="ns1:Label" Name="..." [Comment="..."] /> block.
    // Comment is genuinely optional: the corpus has 8,397 Name attributes but only 8,361 Comments.
    private static readonly Regex KeyRegex = new(@"Key=""(\d+)""", RegexOptions.Compiled);
    private static readonly Regex NameRegex = new(@"\sName=""([^""]*)""", RegexOptions.Compiled);
    private static readonly Regex CommentRegex = new(@"\sComment=""([^""]*)""", RegexOptions.Compiled);

    /// <summary>
    /// Parse the corpus into address -> label.
    /// </summary>
    public static Dictionary<int, IAnnotationLabel> Load()
    {
        var results = new Dictionary<int, IAnnotationLabel>();
        var text = File.ReadAllText(Path);

        // split into per-entry chunks; chunk 0 is the header before the first item.
        var chunks = text.Split("<sys:Item ", StringSplitOptions.None);

        for (var i = 1; i < chunks.Length; i++)
        {
            var chunk = chunks[i];

            var keyMatch = KeyRegex.Match(chunk);
            var nameMatch = NameRegex.Match(chunk);
            if (!keyMatch.Success || !nameMatch.Success)
                continue;

            if (!int.TryParse(keyMatch.Groups[1].Value, out var snesAddress))
                continue;

            var commentMatch = CommentRegex.Match(chunk);

            results[snesAddress] = new Label
            {
                // XML attribute values are entity-encoded: &quot; &amp; and numeric forms like
                // &#x9; (TAB) and &#xA; (LF). Decoding is required or we would be testing against
                // the escaped text rather than the real label content.
                Name = WebUtility.HtmlDecode(nameMatch.Groups[1].Value),
                Comment = commentMatch.Success
                    ? WebUtility.HtmlDecode(commentMatch.Groups[1].Value)
                    : "",
            };
        }

        return results;
    }
}
