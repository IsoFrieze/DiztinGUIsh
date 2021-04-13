using System;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.export
{
    public class DataErrorChecking
    {
        public class DataErrorInfo
        {
            public int Offset { get; init; }
            public string Msg { get; init; }
        }
        public ILogCreatorDataSource Data { get; init; }

        public event EventHandler<DataErrorInfo> ErrorNotifier;

        public void ReportError(int offset, string msg) => 
            ErrorNotifier?.Invoke(this, new DataErrorInfo {Offset = offset, Msg = msg});

        private void ErrorIfOperand(int offset)
        {
            if (Data.GetFlag(offset) == FlagType.Operand)
                ReportError(offset, "Bytes marked as operands formatted as Data.");
        }

        private void ErrorIfAdjacentOperandsSeemWrong(int startingOffset)
        {
            var flag = Data.GetFlag(startingOffset);
            if (flag != FlagType.Operand)
                return;

            var byteLengthFollowing = RomUtil.GetByteLengthForFlag(flag);
            
            for (var i = 1; i < byteLengthFollowing; ++i)
            {
                var expectedFlag = GetFlagButSwapOpcodeForOperand(startingOffset);

                var anyFailed = false;
                anyFailed |= !ErrorIfBoundsLookWrong(startingOffset, i);
                anyFailed |= !ErrorIfUnexpectedFlagAt(startingOffset + i, expectedFlag); 

                // note: use bitwise-or so we don't short circuit. both checks need to run independently so they both report errors
                if (anyFailed)
                    return;
            }
        }
        
        private FlagType GetFlagButSwapOpcodeForOperand(int offset)
        {
            var flag = Data.GetFlag(offset);
            return flag == FlagType.Opcode ? FlagType.Operand : flag;
        }

        private void ErrorIfBranchToNonInstruction(int offset)
        {
            var ia = Data.GetIntermediateAddress(offset, true);
            if (ia >= 0 && IsOpcodeOutboundJump(offset) && !DoesIndirectAddressPointToOpcode(ia))
                ReportError(offset, "Branch or jump instruction to a non-instruction.");
        }

        private bool ErrorIfBoundsLookWrong(int startingOffset, int len)
        {
            if (IsOffsetInRange(startingOffset + len))
                return true;

            var flagDescription = Util.GetEnumDescription(GetFlagButSwapOpcodeForOperand(startingOffset));
            ReportError(startingOffset, $"{flagDescription} extends past the end of the ROM.");
            return false;
        }

        private bool ErrorIfUnexpectedFlagAt(int nextOffset, FlagType expectedFlag)
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
            return Data.GetFlag(Data.ConvertSnesToPc(ia)) == FlagType.Opcode;
        }

        private bool IsOpcodeOutboundJump(int offset)
        {
            return Data.GetFlag(offset) == FlagType.Opcode &&
                   Data.GetInOutPoint(offset) == InOutPoint.OutPoint;
        }

        private bool IsOffsetInRange(int offset)
        {
            return offset >= 0 && offset < Data.GetRomSize();
        }

        public void CheckForErrorsAt(int offset)
        {
            // throw out some errors if stuff looks fishy
            ErrorIfOperand(offset);
            ErrorIfAdjacentOperandsSeemWrong(offset);
            ErrorIfBranchToNonInstruction(offset);
        }   
    }
}