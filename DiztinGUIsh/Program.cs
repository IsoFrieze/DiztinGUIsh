using System;
using System.Windows.Forms;
using DiztinGUIsh.util;

namespace DiztinGUIsh
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var openFile = "";
            if (args.Length > 0)
                openFile = args[0];

            RunNormally(openFile);
        }

        private static void RunNormally(string openFile = "")
        {
            GuiUtil.SetupDPIStuff();

            Application.Run(new DizApplication(openFile));
        }
    }
}