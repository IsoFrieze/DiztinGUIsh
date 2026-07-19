using System.Linq;
using Diz.Core.util;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.Labels;

public class LabelNameValidatorTests
{
    [Theory]
    [InlineData("loop")]
    [InlineData("fn_battle_some_type_of_init_x")]
    [InlineData("UPPER_and_lower_123")]
    [InlineData("has.periods.in.it")]
    [InlineData("label9")]
    [InlineData("status_process_2.5x_evade")]  // real corpus name: segment after '.' starts w/ digit
    [InlineData("_leading_underscore")]
    [InlineData(".sublabel")]           // asar-style sublabel
    [InlineData("+")]                   // asar anonymous forward branch
    [InlineData("-")]                   // asar anonymous backward branch
    [InlineData("++")]
    [InlineData("--")]
    public void ValidNames_AreAccepted(string name) =>
        LabelNameValidator.Validate(name).IsValid.Should().BeTrue();

    // Diz.LogWriter emits `+`/`-` labels WITHOUT a trailing colon precisely because they are the
    // asar anonymous-branch form (AssemblyGenerators.cs:65). They must stay legal.
    [Theory]
    [InlineData("+")]
    [InlineData("-")]
    public void AsarAnonymousBranchLabels_StayLegalUnderBothRuleSets(string name)
    {
        LabelNameValidator.IsValid(name, LabelNameRules.Legacy).Should().BeTrue();
        LabelNameValidator.IsValid(name, LabelNameRules.Strict).Should().BeTrue();
    }

    [Theory]
    [InlineData("has space")]
    [InlineData(" leading_space")]
    [InlineData("trailing_space ")]
    [InlineData(" ")]                   // whitespace-only is NOT the same as empty
    [InlineData("has,comma")]           // would break the CSV round-trip
    [InlineData("has\tTab")]
    [InlineData("has\nNewline")]
    [InlineData("has\"quote")]
    [InlineData("has$dollar")]
    [InlineData("has#hash")]
    [InlineData("has(paren)")]
    [InlineData("has:colon")]
    [InlineData("unicode_é")]                 // asar is ASCII-only: char_props[0x80..0xFF] all 0
    public void InvalidNames_AreRejected(string name)
    {
        var result = LabelNameValidator.Validate(name);
        result.IsValid.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// VERIFIED BY RUNNING ASAR (asar/Release/asar-standalone.exe), not inferred.
    /// '+' and '-' are not label-name characters at all: they are handled by a separate parser,
    /// posneglabelname() (assembleblock.cpp:338-397). Embedded in a name they are a hard error:
    ///     foo-bar:   ->  error: (E5062): Broken label definition.
    ///     foo+bar:   ->  error: (E5062): Broken label definition.
    /// The OLD validator accepted these -- it was too loose. Strict now rejects them.
    /// Legacy still accepts them, deliberately, to preserve historical import behaviour.
    /// </summary>
    [Theory]
    [InlineData("has-hyphens")]
    [InlineData("foo+bar")]
    [InlineData("+-")]      // not a pure run, so not a valid anonymous label either
    [InlineData("foo..bar")] // empty segment; our own conservative tightening
    [InlineData("foo.")]     // trailing dot; our own conservative tightening
    [InlineData("foo[1]")]   // array subscript; our own conservative tightening
    public void NamesAsarRejects_AreRejectedByStrict(string name) =>
        LabelNameValidator.IsValid(name, LabelNameRules.Strict).Should().BeFalse();

    /// <summary>
    /// asar imposes NO length limit -- verified by assembling 1,000- and 10,000-character labels
    /// successfully, and by the absence of any length check in labelname()/confirmname().
    /// </summary>
    [Fact]
    public void NoMaximumLength_IsEnforced()
    {
        LabelNameValidator.MaxLength.Should().Be(0);
        LabelNameValidator.IsValid(new string('L', 10000)).Should().BeTrue();
    }

    /// <summary>
    /// Opcode/register names are NOT reserved in asar: `nop:` assembles, and asar's own test
    /// tests/labela.asm defines a label literally named `a`.
    /// </summary>
    [Theory]
    [InlineData("nop")]
    [InlineData("a")]
    [InlineData("DEAD")]
    public void OpcodeAndRegisterNames_AreNotReserved(string name) =>
        LabelNameValidator.IsValid(name).Should().BeTrue();

    // The original import regex used `*`, not `+`. Empty is therefore legal and MUST stay legal:
    // the CT US corpus contains 23 labels with an empty name (asserted for real in
    // EmptyNames_ExistInTheRealCorpus below).
    [Fact]
    public void EmptyName_IsValid_UnderBothRuleSets()
    {
        LabelNameValidator.Validate("", LabelNameRules.Legacy).IsValid.Should().BeTrue();
        LabelNameValidator.Validate("", LabelNameRules.Strict).IsValid.Should().BeTrue();
        LabelNameValidator.Validate(null).IsValid.Should().BeTrue();
    }

    // The one place Strict is tighter than Legacy.
    [Theory]
    [InlineData("1abc")]
    [InlineData("0")]
    [InlineData("99problems")]
    public void LeadingDigit_RejectedByStrict_AcceptedByLegacy(string name)
    {
        LabelNameValidator.IsValid(name, LabelNameRules.Legacy).Should().BeTrue(
            "Legacy must reproduce the historical import regex exactly");
        LabelNameValidator.IsValid(name, LabelNameRules.Strict).Should().BeFalse();
    }

    [Fact]
    public void ValidationResult_HelpersBehave()
    {
        ValidationResult.Ok.IsValid.Should().BeTrue();
        ValidationResult.Ok.Error.Should().BeNull();
        ValidationResult.Fail("nope").IsValid.Should().BeFalse();
        ValidationResult.Fail("nope").Error.Should().Be("nope");
    }

    // ---------------------------------------------------------------------------------------
    // real corpus
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// HISTORY: the CT US corpus used to contain exactly 2 label names that the import regex
    /// rejected -- "overworld_object_pc2_pointer\t" and "overworld_object_pc3_pointer\t", both
    /// with a trailing TAB. Those were fixed at the source by chronotrigger-disassembly commit
    /// 9c299f8, so the corpus now validates completely under Legacy as well.
    /// </summary>
    [Fact]
    public void Corpus_FullyValidatesUnderLegacy()
    {
        if (!CtLabelCorpus.IsAvailable)
            return; // corpus worktree not present; nothing to assert

        var labels = CtLabelCorpus.Load();
        labels.Count.Should().BeGreaterThan(8000, "the corpus test must not pass vacuously");

        labels.Values
            .Select(x => x.Name)
            .Where(name => !LabelNameValidator.IsValid(name, LabelNameRules.Legacy))
            .Should().BeEmpty();
    }

    /// <summary>
    /// The tightening check the task demanded: does Strict reject anything real that Legacy allows?
    /// Answer, measured: no. Zero corpus names begin with a digit.
    /// </summary>
    [Fact]
    public void Corpus_StrictRejectsNothingThatLegacyAccepts()
    {
        if (!CtLabelCorpus.IsAvailable)
            return;

        var newlyRejected = CtLabelCorpus.Load().Values
            .Select(x => x.Name)
            .Where(name => LabelNameValidator.IsValid(name, LabelNameRules.Legacy)
                           && !LabelNameValidator.IsValid(name, LabelNameRules.Strict))
            .ToList();

        newlyRejected.Should().BeEmpty(
            "tightening to Strict must not break any label that exists in a real project");
    }

    /// <summary>
    /// THE HARD CONSTRAINT: tightening Strict to match asar must not reject any real label.
    /// Every one of the 8,397 CT US corpus names must pass Strict, except exactly the 2
    /// known trailing-TAB ones (which asar also rejects -- `foo\t:` gives "(E5027): Invalid
    /// number", observed -- and which are being fixed separately).
    /// </summary>
    [Fact]
    public void Corpus_EveryNameIsValidUnderStrict_ExceptKnownTabNames()
    {
        if (!CtLabelCorpus.IsAvailable)
            return;

        var labels = CtLabelCorpus.Load();
        labels.Count.Should().BeGreaterThan(8000, "the corpus test must not pass vacuously");

        var failures = labels.Values
            .Select(x => x.Name)
            .Where(name => !LabelNameValidator.IsValid(name, LabelNameRules.Strict))
            .ToList();

        // The corpus is now CLEAN: chronotrigger-disassembly commit 9c299f8 ("labels: strip
        // trailing tab from two overworld pointer label names") removed the last two bad names,
        // so Strict must reject nothing at all. Trailing-TAB rejection is still pinned as a unit
        // case in TrailingTab_IsRejected below.
        failures.Should().BeEmpty(
            "tightening Strict to match asar must not reject any label in a real project");
    }

    /// <summary>
    /// The trailing-TAB names that used to live in the corpus. asar rejects them too:
    /// `foo\t:` -> "error: (E5027): Invalid number." (observed). Pinned here as a unit case now
    /// that the corpus itself has been cleaned.
    /// </summary>
    [Theory]
    [InlineData("overworld_object_pc2_pointer\t")]
    [InlineData("overworld_object_pc3_pointer\t")]
    public void TrailingTab_IsRejected(string name)
    {
        LabelNameValidator.IsValid(name, LabelNameRules.Strict).Should().BeFalse();
        LabelNameValidator.IsValid(name, LabelNameRules.Legacy).Should().BeFalse();
        LabelNameValidator.IsValid(name.Trim(), LabelNameRules.Strict).Should().BeTrue();
    }

    [Fact]
    public void EmptyNames_ExistInTheRealCorpus()
    {
        if (!CtLabelCorpus.IsAvailable)
            return;

        // this is the evidence that AllowEmpty must stay true.
        CtLabelCorpus.Load().Values.Count(x => x.Name.Length == 0)
            .Should().BeGreaterThan(0);
    }
}
