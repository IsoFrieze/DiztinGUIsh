using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.export
{
    public partial class LogCreator
    {
        protected int ErrorCount;

        private void ReportError(int offset, string msg)
        {
            ++ErrorCount;
            var offsetMsg = offset >= 0 ? $" Offset 0x{offset:X}" : "";
            Output.WriteErrorLine($"({ErrorCount}){offsetMsg}: {msg}");
        }

        private void ErrorIfOperand(int offset)
        {
            if (Data.GetFlag(offset) == Data.FlagType.Operand)
                ReportError(offset, "Bytes marked as operands formatted as Data.");
        }

        private void ErrorIfAdjacentOperandsSeemWrong(int offset)
        {
            if (Data.GetFlag(offset) != Data.FlagType.Operand)
                return;

            var byteLengthFollowing = GetByteLengthFollowing(offset);
            var stop = false;
            for (var i = 1; i < byteLengthFollowing && !stop; ++i)
            {
                var expectedFlag = GetFlagButSwapOpcodeForOperand(offset);

                // note: use bitwise-or so we don't short circuit. both checks need to run independently
                stop =
                    !ErrorIfBoundsLookWrong(offset, i) |
                    !ErrorIfUnexpectedFlagAt(offset + i, expectedFlag);
            }
        }

        private void ErrorIfBranchToNonInstruction(int offset)
        {
            var ia = Data.GetIntermediateAddress(offset, true);
            if (ia >= 0 && IsOpcodeOutboundJump(offset) && !DoesIndirectAddressPointToOpcode(ia))
                ReportError(offset, "Branch or jump instruction to a non-instruction.");
        }

        private bool ErrorIfBoundsLookWrong(int offsetStart, int len)
        {
            if (IsOffsetInRange(offsetStart + len))
                return true;

            var flagDescription = Util.GetEnumDescription(GetFlagButSwapOpcodeForOperand(offsetStart));
            ReportError(offsetStart, $"{flagDescription} extends past the end of the ROM.");
            return false;
        }

        private bool ErrorIfUnexpectedFlagAt(int nextOffset, Data.FlagType expectedFlag)
        {
            if (!IsOffsetInRange(nextOffset))
                return false;

            if (Data.GetFlag(nextOffset) == expectedFlag)
                return true;

            var expectedFlagName = Util.GetEnumDescription(expectedFlag);
            var actualFlagName = Util.GetEnumDescription(Data.GetFlag(nextOffset));
            var msg = $"Expected {expectedFlagName}, but got {actualFlagName} instead.";
            ReportError(nextOffset, msg);
            return false;
        }

        private bool DoesIndirectAddressPointToOpcode(int ia)
        {
            return Data.GetFlag(Data.ConvertSnesToPc(ia)) == Data.FlagType.Opcode;
        }

        private bool IsOpcodeOutboundJump(int offset)
        {
            return Data.GetFlag(offset) == Data.FlagType.Opcode &&
                   Data.GetInOutPoint(offset) == Data.InOutPoint.OutPoint;
        }

        private bool IsOffsetInRange(int offset)
        {
            return offset >= 0 && offset < Data.GetRomSize();
        }

        private int GetByteLengthFollowing(int offset)
        {
            var flag = Data.GetFlag(offset);
            return flag == Data.FlagType.Opcode ? GetLineByteLength(offset) : RomUtil.GetByteLengthForFlag(flag);
        }
    }
}