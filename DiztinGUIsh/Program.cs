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
        public static DizApplicationContext.DizApplicationArgs ParseArgs(string[] args)
        {
            var parsedArgs = new DizApplicationContext.DizApplicationArgs();
            
            if (args.Length > 0)
                parsedArgs.FileToOpen = args[0];

            return parsedArgs;
        }
        
        [STAThread]
        public static void Main(string[] args)
        {
            // example stuff you can do:
            // ProfilerDotTrace.Enabled = true;
            //
            args = args.Append(SampleRomHackProjectsController.SampleProjectName).ToArray();
            // or
            // args = args.Append(@"some-test-file.dizraw").ToArray();
            // END TEMP

            var parsedArgs = ParseArgs(args);

            // call before setting up any forms/GUI elements
            GuiUtil.SetupDPIStuff();

            Application.Run(new DizApplicationContext(parsedArgs));
        }
    }
}