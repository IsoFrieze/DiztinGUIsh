using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    public class Manager
    {
        public static int Step(int offset, bool branch)
        {
            switch (Data.GetArchitechture(offset))
            {
                case Data.Architechture.CPU65C816: return CPU65C816.Step(offset, branch);
                case Data.Architechture.APUSPC700: return offset;
                case Data.Architechture.GPUSuperFX: return offset;
            }
            return offset;
        }

        public static int AutoStep(int offset, bool harsh)
        {
            return offset;
        }

        public static int Mark(int offset, Data.FlagType type, int count)
        {
            return offset;
        }
    }
}
