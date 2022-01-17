// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Diz.Core.model;

namespace Diz.Core.util
{
    public static class ChecksumUtil
    {
        #region Diz interface
        
        public static uint ComputeChecksumFromRom(IList<byte> romdata) => 
            AsarChecksumUtil.getchecksum(romdata);
        public static bool IsRomChecksumValid(IList<byte> romdata, RomMapMode mode, int size) => 
            AsarChecksumUtil.goodchecksum(romdata, mode, size);
        public static void UpdateRomChecksum(IList<byte> romdata, RomMapMode mode, int size) => 
            AsarChecksumUtil.fixchecksum(romdata, mode, size);
        
        #endregion

        // try to not modify the code here too much to keep in sync with Asar upstream C++ code,
        // in case they ever have bugfixes/etc.
        // adapted from https://github.com/RPGHacker/asar/blob/master/src/asar/libsmw.cpp
        // original authors: randomdude9999, RPGHacker, CypherSignal, p4plus2, probably others
        private static class AsarChecksumUtil
        {
            #region Checksum implementation, adapted from Asar

            [SuppressMessage("ReSharper", "SuggestVarOrType_BuiltInTypes")]
            internal static uint getchecksum(IList<byte> romdata)
            {
                uint romlen = (uint) romdata.Count;
                uint checksum = 0;
                if ((romlen & (romlen - 1)) == 0)
                {
                    // romlen is a power of 2, just add up all the bytes
                    for (var i = 0; i < romlen; i++)
                        checksum += romdata[i];
                }
                else
                {
                    // assume romlen is the sum of 2 powers of 2 - i haven't seen any real rom that isn't,
                    // and if you make such a rom, fixing its checksum is your problem.
                    uint firstpart = bitround(romlen) >> 1;
                    uint secondpart = romlen - firstpart;
                    uint repeatcount = firstpart / secondpart;
                    uint secondpart_sum = 0;
                    for (int i = 0; i < firstpart; i++) checksum += romdata[i];
                    for (int i = (int) firstpart; i < romlen; i++) secondpart_sum += romdata[i];
                    checksum += secondpart_sum * repeatcount;
                }

                return checksum & 0xFFFF;
            }

            internal static bool goodchecksum(IList<byte> romdata, RomMapMode mode, int size)
            {
                int snestopc(int snesAddress) => RomUtil.ConvertSnesToPc(snesAddress, mode, size);

                var checksum = (int) getchecksum(romdata);
                return ((romdata[snestopc(0x00FFDE)] ^ romdata[snestopc(0x00FFDC)]) == 0xFF) &&
                       ((romdata[snestopc(0x00FFDF)] ^ romdata[snestopc(0x00FFDD)]) == 0xFF) &&
                       ((romdata[snestopc(0x00FFDE)] & 0xFF) == (checksum & 0xFF)) &&
                       ((romdata[snestopc(0x00FFDF)] & 0xFF) == ((checksum >> 8) & 0xFF));
            }

            internal static void fixchecksum(IList<byte> romdata, RomMapMode mode, int size)
            {
                int snestopc(int snesAddress) => RomUtil.ConvertSnesToPc(snesAddress, mode, size);

                // randomdude999: clear out checksum bytes before recalculating checksum, this should make it correct on roms that don't have a checksum yet
                romdata.writeromdata(snestopc(0x00FFDC), 0xFFFF0000);
                var checksum = getchecksum(romdata);
                romdata.writeromdata_byte(snestopc(0x00FFDE), (byte) (checksum & 255));
                romdata.writeromdata_byte(snestopc(0x00FFDF), (byte) ((checksum >> 8) & 255));
                romdata.writeromdata_byte(snestopc(0x00FFDC), (byte) ((checksum & 255) ^ 255));
                romdata.writeromdata_byte(snestopc(0x00FFDD), (byte) (((checksum >> 8) & 255) ^ 255));
            }

            private static uint bitround(uint x)
            {
                if ((x & (x - 1)) == 0) return x;
                while ((x & (x - 1)) != 0) x &= x - 1;
                return x << 1;
            }

            #endregion
        }
        
        #region Diz implementation of Asar functions
        private static void writeromdata_byte(this IList<byte> @this, int pcOffset, byte val) =>
            @this[pcOffset] = val;

        private static void writeromdata(this IList<byte> @this, int pcOffset, uint val32) =>
            ByteUtil.IntegerIntoByteArray(val32, @this, pcOffset);
        #endregion
    }
}