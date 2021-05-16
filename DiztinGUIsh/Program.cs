using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Core;
using Diz.Core.util;
using DiztinGUIsh.util;

namespace DiztinGUIsh
{
    internal static class Program
    {
        private static IDizApplication.Args ParseArgs(IReadOnlyList<string> args)
        {
            var parsedArgs = new IDizApplication.Args();
            
            if (args.Count > 0)
                parsedArgs.FileToOpen = args[0];

            return parsedArgs;
        }
        
        [STAThread]
        public static void Main(string[] args)
        {
            // examples of some stuff you can do:
            // ProfilerDotTrace.Enabled = true; // enable DotTrace profiler
            // args = args.Append(SampleRomHackProjectsController.SampleProjectName).ToArray();
            // args = args.Append(@"some-test-file.dizraw").ToArray();
            // END TEMP

            var parsedArgs = ParseArgs(args);
            
            RegisterTypes();

            // call before setting up any forms/GUI elements
            GuiUtil.SetupDpiStuff();

            Application.Run(new DizApplicationContext(parsedArgs));
        }
        
        private static void RegisterTypes()
        {
            // see https://www.lightinject.net/ for more info on what you can do here
            // we only need to explicitly scan the first assembly. after that, all others are
            // lazy-loaded as needed.  This will look for a class derived from
            // ICompositionRoot in each assembly scanned.
            //
            // Plugins will need to explicitly register themselves with the container on startup
            Service.Container.RegisterAssembly(typeof(RegisterTypesDiztinGuIsh).Assembly);
        }
    }
}