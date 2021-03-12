using System;
using System.Collections.Generic;
using System.Drawing;
using Diz.Core.model;

namespace DiztinGUIsh.window2
{
    
    // TODO: probably should use the service provider pattern for this or something better
    // than just hardcoding it.  still, it's not too bad for a first pass.
    public class CellConditionalFormatterCollection
    {
        private readonly Dictionary<string, CellConditionalFormatter> _formatters = new();

        public void Register(string dataPropertyName, CellConditionalFormatter formatter)
        {
            _formatters.Add(dataPropertyName, formatter);
            formatter.DataPropertyName = dataPropertyName;
        }

        public CellConditionalFormatter Get(string dataPropertyName)
        {
            return _formatters.TryGetValue(dataPropertyName, out var formatter) ? formatter : null;
        }

        public static void RegisterAllRomByteFormattersHelper()
        {
            var formatCollection = new CellConditionalFormatterCollection();
            
            formatCollection.Register("Rom", new CellConditionalFormatter());
            formatCollection.Register("DataBank", new CellConditionalFormatter {IsEditable = true});
            formatCollection.Register("DirectPage", new CellConditionalFormatter {IsEditable = true});
            formatCollection.Register("XFlag", new CellConditionalFormatter());
            formatCollection.Register("MFlag", new CellConditionalFormatter());
            formatCollection.Register("TypeFlag", new CellConditionalFormatter());
            formatCollection.Register("Arch", new CellConditionalFormatter());
            formatCollection.Register("Point", new CellConditionalFormatter());
            formatCollection.Register("Offset", new CellConditionalFormatter());
        }
    }



    public static class CellFormatterUtils
    {
        public static Color DefaultBackgroundColor => Color.White;

        public static Color GetBackColorInOut(RomByteData romByteData)
        {
            int r = 255, g = 255, b = 255;
            if ((romByteData.Point & (InOutPoint.EndPoint | InOutPoint.OutPoint)) != 0) g -= 50;
            if ((romByteData.Point & (InOutPoint.InPoint)) != 0) r -= 50;
            if ((romByteData.Point & (InOutPoint.ReadPoint)) != 0) b -= 50;
            return Color.FromArgb(r, g, b);
        }

        public static Color GetInstructionBackgroundColor(RomByteData romByteData)
        {
            var opcode = romByteData.Rom;
            var isWeirdInstruction =
                    opcode == 0x40 || opcode == 0xCB || opcode == 0xDB || opcode == 0xF8 || // RTI WAI STP SED
                    opcode == 0xFB || opcode == 0x00 || opcode == 0x02 || opcode == 0x42 // XCE BRK COP WDM
                ;
            return isWeirdInstruction ? Color.Yellow : DefaultBackgroundColor;
        }

        public static Color GetDataBankColor(RomByteData romByteData)
        {
            switch (romByteData.Rom)
            {
                // PLB MVP MVN
                case 0xAB:
                case 0x44:
                case 0x54:
                    return Color.OrangeRed;
                // PHB
                case 0x8B:
                    return Color.Yellow;
                default:
                    return DefaultBackgroundColor;
            }
        }

        public static Color GetDirectPageColor(RomByteData romByteAtPaintingRow)
        {
            switch (romByteAtPaintingRow.Rom)
            {
                // PLD TCD
                case 0x2B:
                case 0x5B:
                    return Color.OrangeRed;

                // PHD TDC
                case 0x0B:
                case 0x7B:
                    return Color.Yellow;

                default:
                    return DefaultBackgroundColor;
            }
        }

        public static Color GetMFlagColor(RomByteData romByteAtRow, byte nextByte)
        {
            return GetMXFlagColor(romByteAtRow, true, nextByte);
        }
        
        public static Color GetXFlagColor(RomByteData romByteAtRow, byte nextByte)
        {
            return GetMXFlagColor(romByteAtRow, false, nextByte);
        }

        private static Color GetMXFlagColor(RomByteData romByteAtRow, bool isM, byte nextByte)
        {
            var mask = isM ? 0x20 : 0x10; // M and X handled near identical except for mask
            switch (romByteAtRow.Rom)
            {
                // PLP
                // SEP REP, *iff* relevant bit is set on next byte
                case 0x28:
                case 0xC2 or 0xE2 when (nextByte & mask) != 0:
                    return Color.OrangeRed;
                case 0x08: // PHP
                    return Color.Yellow;
                default:
                    return DefaultBackgroundColor;
            }
        }
    }


    // TODO: eventually, store these as attributes on the RomByte class 
    public class CellConditionalFormatter
    {
        public string DataPropertyName; // matches something in RomByData.{name of the property you want}
        // i.e. for RomByteData.TypeFlag, specify "TypeFlag" here

        public bool IsEditable; // if user can edit this property

        public void SetStyle()
        {
            
            // might not need this fn....

            throw new NotImplementedException();
        }
    }
}