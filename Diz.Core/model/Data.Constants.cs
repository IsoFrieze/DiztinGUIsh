using System;
using System.ComponentModel;
using System.Drawing;

namespace Diz.Core.model
{
    public partial class Data
    {
        [System.AttributeUsage(System.AttributeTargets.All)]
        public class ColorDescriptionAttribute : System.Attribute
        {
            public KnownColor Color { get; }

            public ColorDescriptionAttribute(KnownColor c)
            {
                Color = c;
            }
        }

        public enum FlagType : byte
        {
            [ColorDescription(KnownColor.Black)] Unreached = 0x00,

            [ColorDescription(KnownColor.Yellow)] Opcode = 0x10,

            [ColorDescription(KnownColor.YellowGreen)]
            Operand = 0x11,

            [ColorDescription(KnownColor.NavajoWhite)] [Description("Data (8-bit)")]
            Data8Bit = 0x20,

            [ColorDescription(KnownColor.LightPink)]
            Graphics = 0x21,

            [ColorDescription(KnownColor.PowderBlue)]
            Music = 0x22,

            [ColorDescription(KnownColor.DarkSlateGray)]
            Empty = 0x23,

            [ColorDescription(KnownColor.NavajoWhite)] [Description("Data (16-bit)")]
            Data16Bit = 0x30,

            [ColorDescription(KnownColor.Orchid)] [Description("Pointer (16-bit)")]
            Pointer16Bit = 0x31,

            [ColorDescription(KnownColor.NavajoWhite)] [Description("Data (24-bit)")]
            Data24Bit = 0x40,

            [ColorDescription(KnownColor.Orchid)] [Description("Pointer (24-bit)")]
            Pointer24Bit = 0x41,

            [ColorDescription(KnownColor.NavajoWhite)] [Description("Data (32-bit)")]
            Data32Bit = 0x50,

            [ColorDescription(KnownColor.Orchid)] [Description("Pointer (32-bit)")]
            Pointer32Bit = 0x51,

            [ColorDescription(KnownColor.Aquamarine)]
            Text = 0x60
        }

        public enum Architecture : byte
        {
            [Description("65C816")] CPU65C816 = 0x00,
            [Description("SPC700")] APUSPC700 = 0x01,
            [Description("SuperFX")] GPUSuperFX = 0x02
        }

        [Flags]
        public enum InOutPoint : byte
        {
            InPoint = 0x01,
            OutPoint = 0x02,
            EndPoint = 0x04,
            ReadPoint = 0x08
        }

        public enum ROMMapMode : byte
        {
            LoROM,

            HiROM,

            ExHiROM,

            [Description("SA - 1 ROM")] SA1ROM,

            [Description("SA-1 ROM (FuSoYa's 8MB mapper)")]
            ExSA1ROM,

            SuperFX,

            [Description("Super MMC")] SuperMMC,

            ExLoROM
        }

        public enum ROMSpeed : byte
        {
            SlowROM,
            FastROM,
            Unknown
        }

        public const int LOROM_SETTING_OFFSET = 0x7FD5;
        public const int HIROM_SETTING_OFFSET = 0xFFD5;
        public const int EXHIROM_SETTING_OFFSET = 0x40FFD5;
        public const int EXLOROM_SETTING_OFFSET = 0x407FD5;
    }
}