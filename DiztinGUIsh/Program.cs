using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DiztinGUIsh.util;
using DiztinGUIsh.window2;

namespace DiztinGUIsh
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // TODO: temp hack, removeme
            args = args.Append(SampleRomHackProjectsController.SampleProjectName).ToArray();
            // END HACK

            var Args = new DizApplicationContext.DizApplicationArgs();

            if (args.Length > 0)
                Args.FileToOpen = args[0];

            // call before setting up any forms/GUI elements
            GuiUtil.SetupDPIStuff();

            Application.Run(new DizApplicationContext(Args));
        }
    }
}