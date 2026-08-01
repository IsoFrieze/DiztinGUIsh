namespace Diz.Ui.ViewModels.MisalignmentChecker;

/// <summary>
/// "Check for misaligned flags": sweep the ROM looking for bytes whose flags contradict the
/// instruction or data item they belong to (an operand with no opcode in front of it, the
/// second byte of a two-byte item flagged as something else), and report what was found.
///
/// This ViewModel RUNS a scan it does not own. The real sweep is
/// SnesApiExtensions.GenerateMisalignmentReport in Diz.Cpu.65816 -- an assembly this one is not
/// allowed to reference -- so the caller hands in a delegate at construction and this type only
/// decides when to call it and how to describe the answer. The same separation as the
/// HarshAutoStep command: one implementation, reachable from a window, a script, or an API.
///
/// It also does not FIX anything. Whoever opened it applies ProjectController.FixMisalignedFlags
/// afterwards if the user asked for that, which is why nothing here is gated on having scanned:
/// fixing without scanning first has always been allowed and stays allowed.
///
/// NOTHING RUNS AT CONSTRUCTION. The scan walks the whole ROM, so it happens when
/// <see cref="Scan"/> is called and not a moment earlier; a freshly built ViewModel is empty and
/// says nothing.
/// </summary>
public sealed class MisalignmentCheckerViewModel : ViewModelNotifierBase
{
    /// <summary>
    /// How many findings the underlying report generator collects before it gives up and returns
    /// what it has. Mirrors the constant in GenerateMisalignmentReport; when the reported count
    /// reaches it, the ROM was not swept to the end.
    /// </summary>
    /// <remarks>
    /// The generator tests this at the top of each step, not after each finding, and one step
    /// can produce several findings -- so a capped scan reports AT LEAST this many, not exactly
    /// this many. That is why the capped wording quotes the real count and calls the limit a
    /// stopping point rather than claiming "the first 500".
    /// </remarks>
    public const int FindingLimit = 500;

    private readonly Func<(int found, string reportText)> scan;

    private string reportText = "";
    private int? foundCount;

    /// <param name="scan">
    /// Runs the sweep and returns how many misalignments it found together with the text
    /// describing them. Supplied by the caller because the implementation lives outside the
    /// assemblies this one may reference. Never called until <see cref="Scan"/> is.
    /// </param>
    /// <param name="notificationMarshaller">See <see cref="ViewModelNotifierBase"/>.</param>
    public MisalignmentCheckerViewModel(
        Func<(int found, string reportText)> scan,
        Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        ArgumentNullException.ThrowIfNull(scan);
        this.scan = scan;
    }

    /// <summary>
    /// The report the last scan produced, verbatim -- one line per finding, or the generator's
    /// own "nothing found" sentence. Empty until something has been scanned.
    /// </summary>
    public string ReportText
    {
        get => reportText;
        private set
        {
            reportText = value;
            OnPropertyChanged(nameof(ReportText));
        }
    }

    /// <summary>
    /// How many misalignments the last scan found, or null when nothing has been scanned yet.
    /// Null and 0 are different answers: "not asked" versus "asked, and the ROM is clean".
    /// </summary>
    public int? FoundCount
    {
        get => foundCount;
        private set
        {
            foundCount = value;
            OnPropertyChanged(nameof(FoundCount));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>
    /// One line summarising the last scan, or empty before there has been one. Derived from
    /// <see cref="FoundCount"/> so the two can never disagree.
    /// </summary>
    public string StatusText => DescribeCount(FoundCount);

    /// <summary>
    /// Run the scan and take on its results. Safe to call repeatedly: each call replaces
    /// everything the previous one produced, so a user who fixes something by hand and scans
    /// again sees only the new answer.
    /// </summary>
    public void Scan()
    {
        var (found, text) = scan();

        ReportText = text ?? "";
        FoundCount = found;
    }

    /// <summary>The wording for a given result count; null means no scan has happened.</summary>
    private static string DescribeCount(int? count) => count switch
    {
        null => "",
        0 => "No misalignments found",
        >= FindingLimit =>
            $"Found {count} misalignments (scan stopped at the {FindingLimit}-result limit; " +
            "there may be more)",
        1 => "Found 1 misalignment",
        _ => $"Found {count} misalignments",
    };
}
