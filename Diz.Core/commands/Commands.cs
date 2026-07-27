namespace Diz.Core.commands;

public class MarkCommand
{
    public enum MarkManyProperty
    {
        Flag = 0,
        DataBank = 1,
        DirectPage = 2,
        MFlag = 3,
        XFlag = 4,
        CpuArch = 5,
    };
        
    public MarkManyProperty Property { get; set; }
    public int Start { get; set; }
    public int Count { get; set; }
    public object Value { get; set; }
}

/// <summary>
/// "Harsh auto step": start decoding at <see cref="Start"/> and keep interpreting bytes as
/// opcodes for <see cref="Count"/> bytes, ignoring control flow entirely. Harsh because
/// nothing stops it running off the end of a block of code and straight into data.
///
/// Describes the request only; applying it is SnesApiExtensions.ApplyAutoStepHarshCommand in
/// Diz.Cpu.65816. Same split as <see cref="MarkCommand"/>: the command can be built by a
/// window, a script, or an external API, and one applier executes all of them.
/// </summary>
public class AutoStepHarshCommand
{
    /// <summary>ROM file offset to start decoding at.</summary>
    public int Start { get; set; }

    /// <summary>
    /// How many bytes to cover, measured forward from <see cref="Start"/>. Decoding stops at
    /// the first instruction that begins at or past Start + Count, so the last instruction
    /// decoded may reach a little further. Zero or less decodes nothing.
    /// </summary>
    public int Count { get; set; }
}