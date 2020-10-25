using System;
using System.Diagnostics;
using System.Windows.Forms;
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
                window.OpenProject("");

            Application.Run(window);
        }
    }
}