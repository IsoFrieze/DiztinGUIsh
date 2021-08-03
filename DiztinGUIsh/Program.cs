using System;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.window;
using JetBrains.Profiler.SelfApi;
using Application = System.Windows.Forms.Application;
using Trace = System.Diagnostics.Trace;

namespace DiztinGUIsh
{
    internal static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            Trace.WriteLine("Diz is starting up");

            var openFile = "";
            if (args.Length > 0)
                openFile = args[0];
            
            // DONT CHECK IN. DEBUG ONLY
            //openFile = "SAMPLE";

            RunNormally(openFile);
        }

        private static void RunNormally(string openFile = "")
        {
            InitPlatformSpecificUi();

            var window = new MainWindow();

            if (openFile == "SAMPLE")
            {
                window.ProjectController.OpenSampleProject();

                window.Show();

                // TODO: DEBUG ONLY. show the labels window
                window.ShowLabelsListView();
            } 
            else if (!string.IsNullOrEmpty(openFile))
            {
                window.ProjectController.OpenProject("");
            }

            Application.Run(window); // winforms
        }

        private static void InitPlatformSpecificUi()
        {
            InitWinforms();

            // enable Avalonia UI support, eventually we'll be phasing out winforms and just using this.
#if USE_AVALONIA_WINFORMS_EMBED
            DizAvalonia.InitAvaloniaEmbeddedInWinforms();
#endif
        }

        private static void InitWinforms()
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                SetProcessDPIAware();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }
    }
}