using System.Text.RegularExpressions;

namespace Diz.Core.util;

/// <summary>
/// Result of validating a value, returned as a VALUE rather than thrown.
/// (the old import path threw InvalidDataException; the in-grid editor did nothing at all)
/// </summary>
public readonly record struct ValidationResult(bool IsValid, string? Error)
{
    public static ValidationResult Ok => new(true, null);
    public static ValidationResult Fail(string msg) => new(false, msg);
}

/// <summary>
/// Which rule set to apply when validating a label name.
/// </summary>
public enum LabelNameRules
{
    /// <summary>
    /// Byte-for-byte identical to the rule <see cref="Diz.Import"/>'s LabelImporter has always
    /// enforced: regex ^([a-zA-Z0-9_\-+\.\-]*)$ . Empty string is ALLOWED (the regex uses * not +).
    /// Use this at the import call site so import behaviour does not change.
    /// </summary>
    Legacy,

    /// <summary>
    /// What asar will actually accept (verified against asar's source AND its shipped binary --
    /// see the citation block on <see cref="LabelNameValidator"/>).
    /// Deliberately a little tighter than asar in the corners; never looser.
    /// Use this for NEW call sites (e.g. the in-grid editor), which today validate nothing at all,
    /// so there is no existing behaviour to regress.
    /// </summary>
    Strict,
}

/// <summary>
/// Single shared home for "is this a legal label name?".
///
/// WHY THIS EXISTS: the rule was duplicated and divergent. Import enforced a regex and threw;
/// the WinForms in-grid editor enforced literally nothing (LabelsView.cs had
/// `// todo (validate for valid label characters)`), so you could hand-type a label that the
/// importer would then refuse to re-import.
///
/// WHY IT LIVES IN Diz.Core: Diz.Import, Diz.LogWriter, Diz.Controllers and the planned
/// Diz.Ui.ViewModels all reference Diz.Core, and none of them reference each other in a way
/// that would let the validator live in Diz.Import and still be reachable from all of them.
/// Diz.Core is the only assembly every consumer can already see.
///
/// ==========================================================================================
/// ASAR LABEL GRAMMAR -- VERIFIED, NOT GUESSED
/// ==========================================================================================
/// asar's full C++ source is available locally at
///     C:\projects\romhax\main\cthack\src\asar\src\asar\
/// and a built binary at  ...\asar\asar\Release\asar-standalone.exe .
/// DO NOT GUESS AT THESE RULES AGAIN -- read the source, or just run the assembler.
///
/// The character predicate is `is_ualnum` (libstr.h:16 -- `char_props[c] & 0x68`). Reading the
/// table at libstr.cpp:591-609, the bits resolve to EXACTLY [A-Za-z0-9_]:
///   - '_' (0x5F) = 0x08                     -> in the set
///   - '-' (0x2D), '+' (0x2B), '.' (0x2E) = 0x00 -> NOT in the set
///   - every byte >= 0x80 = 0x00             -> ASCII only; no Unicode identifiers
/// Case is preserved and significant: labels are stored/looked up verbatim (assembleblock.cpp:500)
/// and `to_lower` is never applied to a label name.
///
/// Rules actually enforced, with citations:
///   1. A leading digit is REJECTED.
///      assembleblock.cpp:420  `if (is_digit(*deref_rawname)) asar_throw_error(... invalid_label_name)`
///      (also confirmname() at assembleblock.cpp:330 for macro/struct/function/namespace names.)
///      OBSERVED: `9foo:` -> "error: (E5059): Invalid label name."
///   2. The first char of each name/segment must be is_ualnum -- assembleblock.cpp:429 and :460.
///      Combined with rule 1 that means the first char is effectively [A-Za-z_].
///   3. Body chars: `while (is_ualnum(c) || c == '.' || c == '[')` -- assembleblock.cpp:462.
///      So '.' IS legal inside a name (it is the sublabel / struct namespace separator), and
///      '[' opens an array subscript. We do NOT allow '[' -- see "where we are tighter" below.
///   4. LEADING dots are the sublabel form: `for (i=0; *p=='.'; i++) p++;` assembleblock.cpp:428.
///      A sublabel requires a parent label to already exist, else error_id_label_missing_parent
///      (assembleblock.cpp:433). OBSERVED: `.orphan:` alone -> "(E5076): This label has no parent."
///      That is CONTEXT-dependent, so a name-only validator cannot check it. We accept leading
///      dots and leave the parent check to asar.
///   5. '+' / '-' are NOT name characters. They are the anonymous-branch form, parsed by a
///      completely separate function, posneglabelname() (assembleblock.cpp:338-397), which
///      consumes a run of the SAME character: `for (depth=0; label[0]==first; depth++) label++;`
///      (line 355). So `+`, `++`, `+++`, `-`, `--` are legal; `+-` is not, and a '+' or '-'
///      EMBEDDED in a name is a hard error.
///      OBSERVED: `foo-bar:` and `foo+bar:` -> "error: (E5062): Broken label definition."
///               `+:` -> assembles fine.
///   6. NO MAXIMUM LENGTH. There is no length check anywhere in labelname()/confirmname(); the
///      name accumulates into a growable `string`. OBSERVED: labels of 1,000 and 10,000
///      characters both assemble without error. So MaxLength stays 0 (no limit).
///   7. Opcode and register names are NOT reserved. OBSERVED: `nop:` assembles fine, and asar's
///      own test tests/labela.asm defines a label literally named `a`. Hex-looking names like
///      `DEAD:` are also fine (a bare number needs a '$'/'%' sigil, so there is no ambiguity).
///   8. Definition vs reference use the SAME grammar -- both go through labelname()
///      (definition: assembleblock.cpp:841; reference: labelvalcore() at :498).
///
/// Related-but-separate grammars, for completeness (all stricter than plain labels):
///   - macro / struct / namespace / function names use confirmname() (assembleblock.cpp:327-336):
///     [A-Za-z_][A-Za-z0-9_]* -- NO dots at all.
///   - `!defines` use validatedefinename() (main.cpp:337-346): is_ualnum only, and note it does
///     NOT reject a leading digit, so `!9` is a legal define name though `9:` is not a legal label.
///   - `?name` is the macro-local prefix (assembleblock.cpp:402) and `#name` a static macro label;
///     both are sigils stripped before the name is parsed, not name characters.
///
/// WHERE WE ARE DELIBERATELY TIGHTER THAN ASAR (all of these are our own conservative choice,
/// taken because the goal is "never accept what asar would reject or silently misparse", and
/// because zero labels in the CT US corpus use any of them):
///   - we reject '[' / ']' array subscripts. asar allows them in a name; a Diz label containing
///     one would be an array reference, not a name.
///   - we require each dot-separated segment to be non-empty, so `foo..bar` and a trailing `foo.`
///     are rejected. asar tolerates both, but they are almost certainly a typo.
///   - we require an anonymous label to be a pure run of one character (`+++`, `---`), matching
///     posneglabelname()'s own run rule, rather than any mix.
///
/// CORPUS CHECK: this rule accepts all 8,397 names in the CT US corpus except exactly the 2
/// known-bad trailing-TAB ones, which it correctly rejects. That includes the 23 empty names and
/// the 3 dotted names (`.start_animation`, `.loop_start`, `status_process_2.5x_evade`).
/// ==========================================================================================
/// </summary>
public static class LabelNameValidator
{
    // NOTE on the original expression ^([a-zA-Z0-9_\-+\.\-]*)$ :
    //   - the `\-` appears TWICE. inside a character class that is a harmless duplicate, not a
    //     range and not a bug -- the accepted character set is unaffected. removed here.
    //   - `*` (not `+`) means the EMPTY string is accepted. that is load-bearing, not an oversight:
    //     see AllowEmpty below.
    // the accepted set is therefore exactly: A-Z a-z 0-9 _ - + .
    private static readonly Regex ValidLabelChars =
        new(@"^[a-zA-Z0-9_+.\-]*$", RegexOptions.Compiled);

    private const string InvalidCharsMessage =
        "Label names may only contain letters, digits, underscore, hyphen, plus, and period.";

    /// <summary>
    /// Empty names are VALID, deliberately, under every rule set. Evidence, not assumption:
    ///   1. the original import regex used `*`, so import has always accepted them;
    ///   2. the CT US corpus (00004_save_Labels.txt) contains 23 labels with Name="";
    ///   3. Diz.LogWriter special-cases them on the way out --
    ///      AssemblyGenerators.cs:65  `var noColon = label.Length == 0 || ...`
    /// Rejecting them would break a real project, which is worse than a loose rule.
    /// Note that a name of " " (whitespace) is still REJECTED: space is not in the character
    /// class, and that was true before this class existed too.
    /// </summary>
    public const bool AllowEmpty = true;

    /// <summary>
    /// There is NO maximum length, and this is now VERIFIED rather than merely "not found":
    /// asar's labelname() (assembleblock.cpp:399-494) and confirmname() (:327-336) contain no
    /// length check at all, and asar assembles labels of 1,000 and 10,000 characters without
    /// complaint (observed by running asar-standalone.exe). Diz imposes no cap either, and the
    /// longest name in the CT US corpus is 60 chars. No cap is invented here.
    /// </summary>
    public const int MaxLength = 0; // 0 == no limit

    /// <summary>
    /// The asar anonymous-branch labels: a run of one or more of the SAME sigil.
    /// Mirrors posneglabelname()'s run loop (assembleblock.cpp:355). These are load-bearing for
    /// Diz: Diz.LogWriter emits such labels WITHOUT a trailing colon precisely because they are
    /// this form -- AssemblyGenerators.cs:65 `label[0] == '-' || label[0] == '+'`.
    /// </summary>
    private static readonly Regex AnonymousBranchLabel =
        new(@"^(?:\++|-+)$", RegexOptions.Compiled);

    /// <summary>
    /// A plain label name: optional leading dots (sublabel nesting, assembleblock.cpp:428), then
    /// an identifier, then optional dot-separated segments. Segments after the first may begin
    /// with a digit -- asar's body loop (assembleblock.cpp:462) accepts any is_ualnum there, and
    /// the corpus relies on it (`status_process_2.5x_evade`).
    /// </summary>
    private static readonly Regex PlainLabelName =
        new(@"^\.*[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+)*$", RegexOptions.Compiled);

    /// <summary>
    /// Validate a label name.
    /// </summary>
    /// <param name="name">the proposed name. null is treated as empty.</param>
    /// <param name="rules">
    /// <see cref="LabelNameRules.Strict"/> by default. Pass <see cref="LabelNameRules.Legacy"/>
    /// to get exactly the historical import behaviour.
    /// </param>
    public static ValidationResult Validate(string? name, LabelNameRules rules = LabelNameRules.Strict)
    {
        name ??= "";

        if (name.Length == 0)
            return ValidationResult.Ok; // see AllowEmpty

        if (rules == LabelNameRules.Legacy)
        {
            // historical import behaviour, reproduced bug-for-bug on purpose. NOTE: this is LOOSER
            // than asar -- it accepts e.g. "foo-bar", which asar rejects outright with
            // "(E5062): Broken label definition." Kept only so importing an existing .csv does not
            // start failing on data it has always accepted. New call sites should use Strict.
            return ValidLabelChars.IsMatch(name)
                ? ValidationResult.Ok
                : ValidationResult.Fail(InvalidCharsMessage);
        }

        // ---- Strict: what asar will actually accept. See the citation block on this class. ----

        // asar anonymous branch labels (`+`, `++`, `-`, `--`): a separate grammar entirely,
        // parsed by posneglabelname() (assembleblock.cpp:338-397). Checked first, because they
        // are not identifiers and would otherwise fail the identifier rule below.
        if (AnonymousBranchLabel.IsMatch(name))
            return ValidationResult.Ok;

        // VERIFIED against asar, not inferred: `9foo:` -> "(E5059): Invalid label name",
        // thrown at assembleblock.cpp:420. Checked before the general rule so the user gets the
        // specific message rather than a generic one.
        if (char.IsAsciiDigit(name[0]))
            return ValidationResult.Fail("Label names may not begin with a digit.");

        if (!PlainLabelName.IsMatch(name))
            return ValidationResult.Fail(
                "Label names may only contain letters, digits and underscore, optionally " +
                "separated by periods (asar sublabel/struct syntax). '+' and '-' are only legal " +
                "as a standalone anonymous branch label such as '+' or '--'.");

        return ValidationResult.Ok;
    }

    /// <summary>
    /// Convenience: does this name pass, under the given rules?
    /// </summary>
    public static bool IsValid(string? name, LabelNameRules rules = LabelNameRules.Strict) =>
        Validate(name, rules).IsValid;
}
