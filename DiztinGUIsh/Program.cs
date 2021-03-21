using System;
using System.Linq;
using System.Windows.Forms;
using Diz.Core.util;
using DiztinGUIsh.util;
using DiztinGUIsh.window2;

namespace DiztinGUIsh
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // TEMP: Enable this
            ProfilerDotTrace.Enabled = true;
            // args = args.Append(SampleRomHackProjectsController.SampleProjectName).ToArray();
            args = args.Append(@"D:\projects\cthack\src\rom\Chrono Trigger US.dizraw").ToArray();
            // END TEMP

            var Args = new DizApplicationContext.DizApplicationArgs();

            if (args.Length > 0)
                Args.FileToOpen = args[0];

            // call before setting up any forms/GUI elements
            GuiUtil.SetupDPIStuff();

            Application.Run(new DizApplicationContext(Args));
        }
    }
}