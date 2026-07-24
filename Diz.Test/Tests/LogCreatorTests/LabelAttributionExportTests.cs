using Diz.Core;
using Diz.Core.export;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.LogWriter;
using Diz.LogWriter.util;
using Diz.Test.Utils;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test.Tests.LogCreatorTests;

/// <summary>
/// Export-side attribution: labels carry an Author and a free-form Confidence level string. This
/// verifies that those surface in the extra label outputs:
///   - all-labels.csv gains always-present Author/Confidence columns (unspecified "" => blank cell).
///
/// The export-manifest.yaml step is skipped in single-file mode (it has no sidecar file), so it is
/// not exercised here. Manifest coverage was temporarily removed and is to be re-added later with a
/// multi-file export harness.
///
/// In OutputToString + SingleFile mode every "file" the exporter switches to is concatenated
/// into one string (SwitchToStream is a no-op there), so a single AssemblyOutputStr carries the
/// CSV rows alongside the assembly -- the same harness the excluded-author tests use.
/// </summary>
public class LabelAttributionExportTests : ContainerFixture
{
    [Inject] private readonly IDataFactory dataFactory = null!;

    private readonly ITestOutputHelper output;

    public LabelAttributionExportTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    // a tiny but valid LoROM program so the exporter has something to walk.
    private static RomBytes MinimalProgram() => new()
    {
        // CODE_808000: LDA.W $805B,X
        new()
        {
            Rom = 0xBD, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.InPoint, DataBank = 0x80,
            DirectPage = 0x2100
        },
        new() { Rom = 0x5B, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
        new() { Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
        // RTS
        new() { Rom = 0x60, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.OutPoint, DataBank = 0x80, DirectPage = 0x2100 },
    };

    private Data NewData()
    {
        var data = dataFactory.Create();
        data.RomMapMode = RomMapMode.LoRom;
        data.RomSpeed = RomSpeed.FastRom;
        data.RomBytes = MinimalProgram();
        return data;
    }

    private LogCreatorOutput.OutputResult Export(Data data, bool includeUnusedLabels, params string[] excludedAuthors)
    {
        var logCreator = new LogCreator
        {
            Data = new LogCreatorByteSource(data),
            Settings = new LogWriterSettings
            {
                OutputToString = true,
                Structure = LogWriterSettings.FormatStructure.SingleFile,
                SuppressSingleFileModeDisabledError = true,
                IncludeUnusedLabels = includeUnusedLabels,
                ExcludedLabelAuthors = excludedAuthors,
            }
        };
        return logCreator.CreateLog();
    }

    [Fact]
    public void Csv_AuthoredLabel_PopulatesAuthorAndConfidenceColumns()
    {
        var data = NewData();
        data.Labels.AddLabel(0x7E0010,
            new Label { Name = "authored_label", Comment = "cool", Author = "Carol", Confidence = "VeryHigh" });

        var result = Export(data, includeUnusedLabels: true);
        output.WriteLine(result.AssemblyOutputStr);

        result.ErrorCount.Should().Be(0);

        // columns present, in order, right after Comment.
        result.AssemblyOutputStr.Should().Contain("Comment,Author,Confidence,UsedStatus",
            "the CSV header carries Author/Confidence between Comment and UsedStatus");

        // the authored label's row carries both values.
        result.AssemblyOutputStr.Should().Contain(",Carol,VeryHigh,",
            "the authored label's Author and Confidence columns are populated");
    }

    [Fact]
    public void Csv_UnannotatedLabel_LeavesAuthorAndConfidenceBlank_AndNoneNeverRendersAsText()
    {
        var data = NewData();
        // neither author nor a stated confidence.
        data.Labels.AddLabel(0x7E0020, new Label { Name = "bare_label" });

        var result = Export(data, includeUnusedLabels: true);
        output.WriteLine(result.AssemblyOutputStr);

        result.ErrorCount.Should().Be(0);

        // Name then empty Comment/Author/Confidence cells: "bare_label,,,,".
        result.AssemblyOutputStr.Should().Contain("bare_label,,,,",
            "a label with no comment/author/confidence produces blank cells");

        // Confidence None must render as "" -- never the literal text "None".
        result.AssemblyOutputStr.Should().NotContain(",None,",
            "Confidence None renders as an empty string, not the text \"None\"");
    }
}
