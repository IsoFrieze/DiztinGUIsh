using System.Diagnostics.CodeAnalysis;

namespace Diz.Cpu._65816.import;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class SnesVectorNames
{
    public const string Native_COP = "Native_COP";
    public const string Native_BRK = "Native_BRK";
    public const string Native_ABORT = "Native_ABORT";
    public const string Native_NMI = "Native_NMI";
    public const string Native_RESET = "Native_RESET";
    public const string Native_IRQ = "Native_IRQ";
    public const string Emulation_COP = "Emulation_COP";
    public const string Emulation_Unknown = "Emulation_Unknown";
    public const string Emulation_ABORT = "Emulation_ABORT";
    public const string Emulation_NMI = "Emulation_NMI";
    public const string Emulation_RESET = "Emulation_RESET";
    public const string Emulation_IRQBRK = "Emulation_IRQBRK";
}