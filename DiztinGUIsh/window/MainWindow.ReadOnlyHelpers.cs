﻿using Diz.Core.model;
using Diz.Cpu._65816;

namespace DiztinGUIsh.window
{
    public partial class MainWindow
    {
        private int FindIntermediateAddress(int offset)
        {
            if (!RomDataPresent())
                return -1;

            var ia = Project.Data.GetSnesApi().GetIntermediateAddressOrPointer(offset);
            if (ia < 0)
                return -1;

            return Project.Data.ConvertSnesToPc(ia);
        }

        private bool FindUnreached(int offset, bool fromEnd, bool direction, out int unreached)
        {
            var size = Project.Data.GetRomSize();
            unreached = fromEnd ? (direction ? 0 : size - 1) : offset;

            if (direction)
            {
                if (!fromEnd)
                    while (unreached < size - 1 && IsUnreached(unreached))
                        unreached++;
                
                while (unreached < size - 1 && IsReached(unreached)) 
                    unreached++;
            }
            else
            {
                if (unreached > 0) 
                    unreached--;
                
                while (unreached > 0 && IsReached(unreached)) 
                    unreached--;
            }

            while (unreached > 0 && IsUnreached(unreached - 1)) 
                unreached--;

            return IsUnreached(unreached);
        }

        private bool IsReached(int offset)
        {
            return Project.Data.GetSnesApi().GetFlag(offset) != FlagType.Unreached;
        }

        private bool IsUnreached(int offset)
        {
            return Project.Data.GetSnesApi().GetFlag(offset) == FlagType.Unreached;
        }

        private bool RomDataPresent()
        {
            return Project?.Data?.GetRomSize() > 0;
        }

        private bool IsOffsetInRange(int offset)
        {
            return offset >= 0 && offset < Project.Data.GetRomSize();
        }
    }
}