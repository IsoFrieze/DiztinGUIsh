using System.Linq;
using Diz.Core;
using Diz.Core.export;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Cpu._65816;
using Diz.LogWriter;
using Diz.LogWriter.util;
using Diz.Test.Utils;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test.Tests.LogCreatorTests;

/// <summary>
/// PHASE 2b: exporting with LogWriterSettings.ExcludedLabelAuthors set must FULLY hide any label
/// whose Author is in that (case-insensitive) set -- gone from the label listing AND from operand
/// naming, so no operand can dangle a reference to a hidden label.
///
/// Fixture mirrors LogCreatorTests.CreateSampleData: a LoROM program where "LDA.W Test_Data,X" at
/// $808000 names a label ("Test_Data") that lives at the instruction's intermediate address
/// ($80805B). That single label therefore shows up in BOTH places we care about:
///   - as the operand text of the LDA instruction, and
///   - as its own "Test_Data = $80805B" assignment line in the listing.
/// So a single "output must not contain Test_Data" assertion proves both effects at once, and the
/// operand instead falls back to the raw hex address ($80805B) -- no dangling symbol.
/// </summary>
public class ExcludedLabelAuthorTests : ContainerFixture
{
    [Inject] private readonly IDataFactory dataFactory = null!;

    private readonly ITestOutputHelper output;

    public ExcludedLabelAuthorTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private const int TestDataLabelAddr = 0x808000 + 0x5B; // "Test_Data" -- IA of the LDA operand
    private const int Test22LabelAddr = 0x808000 + 0x06;   // "Test22"    -- an unrelated code label

    private Data CreateSampleData(string testDataAuthor)
    {
        var data = dataFactory.Create();

        data.RomMapMode = RomMapMode.LoRom;
        data.RomSpeed = RomSpeed.FastRom;
        data.RomBytes = new RomBytes
        {
            // CODE_808000: LDA.W Test_Data,X
            new()
            {
                Rom = 0xBD, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.InPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },
            new() { Rom = 0x5B, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 }, // Test_Data
            new() { Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 }, // Test_Data

            // STA.W $0100,X
            new() { Rom = 0x9D, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x01, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },

            // DEX
            new() { Rom = 0xCA, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100 },

            // BPL CODE_808000
            new()
            {
                Rom = 0x10, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.OutPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },
            new() { Rom = 0xF7, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
        };

        data.Labels.AddLabel(Test22LabelAddr, new Label { Name = "Test22" });
        data.Labels.AddLabel(TestDataLabelAddr,
            new Label { Name = "Test_Data", Comment = "Pretty cool huh?", Author = testDataAuthor });
        return data;
    }

    private LogCreatorOutput.OutputResult Export(Data data, params string[] excludedAuthors)
    {
        var logCreator = new LogCreator
        {
            Data = new LogCreatorByteSource(data),
            Settings = new LogWriterSettings
            {
                OutputToString = true,
                Structure = LogWriterSettings.FormatStructure.SingleFile,
                SuppressSingleFileModeDisabledError = true,
                ExcludedLabelAuthors = excludedAuthors,
            }
        };
        return logCreator.CreateLog();
    }

    [Fact]
    public void WithoutExclusion_TheAuthoredLabelIsUsedForOperandAndListing()
    {
        // control: no exclusion -> the label is present as operand AND as an assignment line.
        var result = Export(CreateSampleData(testDataAuthor: "Alice"));
        output.WriteLine(result.AssemblyOutputStr);

        result.ErrorCount.Should().Be(0);
        result.AssemblyOutputStr.Should().Contain("LDA.W Test_Data,X", "the operand names the label");
        result.AssemblyOutputStr.Should().Contain("Test_Data = $80805B", "the label listing includes it");
    }

    [Fact]
    public void ExcludingTheAuthor_HidesLabelFromListingAndOperand()
    {
        var result = Export(CreateSampleData(testDataAuthor: "Alice"), "Alice");
        output.WriteLine(result.AssemblyOutputStr);

        result.ErrorCount.Should().Be(0, "hiding a label must not produce export errors");

        // fully invisible: not in the listing, and not naming any operand (no dangling symbol).
        result.AssemblyOutputStr.Should().NotContain("Test_Data",
            "the excluded author's label must vanish from BOTH the listing and operand naming");

        // the operand falls back to the raw hex address (16-bit absolute operand form) instead of
        // dangling -- proving the exclusion reaches operand naming, not just the label listing.
        result.AssemblyOutputStr.Should().Contain("LDA.W $805B,X",
            "with its label hidden, the LDA operand uses the raw hex address");

        // an untouched label (different/empty author) is unaffected.
        result.AssemblyOutputStr.Should().Contain("Test22", "only the excluded author's labels are hidden");
    }

    [Fact]
    public void ExclusionIsCaseInsensitive()
    {
        var result = Export(CreateSampleData(testDataAuthor: "Alice"), "alice");
        output.WriteLine(result.AssemblyOutputStr);

        result.ErrorCount.Should().Be(0);
        result.AssemblyOutputStr.Should().NotContain("Test_Data", "author matching is case-insensitive");
    }

    [Fact]
    public void SettingsEqualityComparesExcludedAuthorsByContentNotReference()
    {
        // ProjectController.UpdateExportSettings uses .Equals to detect changed settings. Two
        // distinct collection instances with the same authors must compare EQUAL (else every
        // export dialog visit would spuriously flag "unsaved changes").
        var a = new LogWriterSettings { ExcludedLabelAuthors = new[] { "Alice", "Bob" } };
        var b = new LogWriterSettings { ExcludedLabelAuthors = new[] { "Alice", "Bob" } };
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());

        // normalization: order, case-dupes, blanks, and surrounding whitespace don't matter.
        var c = new LogWriterSettings { ExcludedLabelAuthors = new[] { " Bob ", "", "Alice", "alice" } };
        c.Should().Be(a, "normalized (sorted, trimmed, de-duped, blanks dropped) blocklists are equal");

        // a genuinely different blocklist is NOT equal.
        var d = new LogWriterSettings { ExcludedLabelAuthors = new[] { "Alice" } };
        d.Should().NotBe(a);

        // round-trips through the collection view.
        a.ExcludedLabelAuthors.Should().BeEquivalentTo(new[] { "Bob", "Alice" });
    }

    [Fact]
    public void ExcludingADifferentAuthor_LeavesTheLabelVisible()
    {
        // excluding an author nobody used must not hide anything.
        var result = Export(CreateSampleData(testDataAuthor: "Alice"), "SomeoneElse");
        output.WriteLine(result.AssemblyOutputStr);

        result.ErrorCount.Should().Be(0);
        result.AssemblyOutputStr.Should().Contain("Test_Data", "no label had the excluded author");
    }
}
