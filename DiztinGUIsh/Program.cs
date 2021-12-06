using System;
using System.Windows.Forms;
using Diz.Core.util;
using DiztinGUIsh.window;

namespace DiztinGUIsh
{
    internal static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
        
        [STAThread]
        static void Main(string[] args)
        {
            var openFile = "";
            if (args.Length > 0)
                openFile = args[0];
            
            RegisterTypes();
            
            RunNormally(openFile);
        }

        private static void RunNormally(string openFile = "")
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                SetProcessDPIAware();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var window = new MainWindow();

            if (openFile != "")
                window.ProjectController.OpenProject("");

            Application.Run(window);
        }
        
        private static void RegisterTypes()
        {
            // see https://www.lightinject.net/ for more info on what you can do here
            // we only need to explicitly scan the first assembly. after that, all others are
            // lazy-loaded as needed.  This will look for a class derived from
            // ICompositionRoot in each assembly scanned.
            //
            // Plugins will need to explicitly register themselves with the container on startup
            Service.Container.RegisterFrom<DizUiCompositionRoot>();
            // Service.Container.RegisterFrom<DizControllersCompositionRoot>();
        }
    }
}