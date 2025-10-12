using System.ComponentModel;
using System.Drawing;

namespace Diz.Core.Interfaces;

[AttributeUsage(AttributeTargets.All)]
public class ColorDescriptionAttribute(uint hexColor) : Attribute {
	public uint HexColor { get; } = hexColor;
}

public enum FlagType : byte
{
	[ColorDescription(0xD3D3D3)] // LightGray
	Unreached = 0x00,

	[ColorDescription(0xFFFF00)] // Yellow
	Opcode = 0x10,

	[ColorDescription(0x9ACD32)] // YellowGreen
	Operand = 0x11,

	[ColorDescription(0xFFDEAD)] [Description("Data (8-bit)")] // NavajoWhite (base data color)
	Data8Bit = 0x20,

	[ColorDescription(0xFFB6C1)] // LightPink
	Graphics = 0x21,

	[ColorDescription(0xB0E0E6)] // PowderBlue
	Music = 0x22,

	[ColorDescription(0x2F4F4F)] // DarkSlateGray
	Empty = 0x23,

	[ColorDescription(0xE6C898)] [Description("Data (16-bit)")] // Slightly darker shade of Data base
	Data16Bit = 0x30,

	[ColorDescription(0xDA70D6)] [Description("Pointer (16-bit)")] // Orchid (base pointer color)
	Pointer16Bit = 0x31,

	[ColorDescription(0xCDB285)] [Description("Data (24-bit)")] // Darker shade of Data base
	Data24Bit = 0x40,

	[ColorDescription(0xC55DC2)] [Description("Pointer (24-bit)")] // Darker shade of Pointer base
	Pointer24Bit = 0x41,

	[ColorDescription(0xB49C72)] [Description("Data (32-bit)")] // Darkest shade of Data base
	Data32Bit = 0x50,

	[ColorDescription(0xB04AAD)] [Description("Pointer (32-bit)")] // Darkest shade of Pointer base
	Pointer32Bit = 0x51,

	[ColorDescription(0x7FFFD4)] // Aquamarine
	Text = 0x60
}

public enum Architecture : byte
{
	[Description("65C816")] Cpu65C816 = 0x00,
	[Description("SPC700")] Apuspc700 = 0x01,
	[Description("SuperFX")] GpuSuperFx = 0x02
}

[Flags]
public enum InOutPoint : byte
{
	None = 0x00,
	InPoint = 0x01,
	OutPoint = 0x02,
	EndPoint = 0x04,
	ReadPoint = 0x08
}
    
public enum RomSpeed : byte
{
	SlowRom,
	FastRom,
	Unknown
}
    
public enum RomMapMode : byte
{
	LoRom,

	HiRom,

	ExHiRom,

	[Description("SA - 1 ROM")] Sa1Rom,

	[Description("SA-1 ROM (FuSoYa's 8MB mapper)")]
	ExSa1Rom,

	SuperFx,

	[Description("Super MMC")] SuperMmc,

	ExLoRom
}
	
public enum ColumnType : int
{
	Label,
	Offset,
	AsciiCharRep,
	NumericRep,
	Point,
	Instruction,
	IA,
	TypeFlag,
	DataBank,
	DirectPage,
	MFlag,
	XFlag,
	Comment
}