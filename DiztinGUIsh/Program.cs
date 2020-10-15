using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Windows.Forms;
using Diz.Core.util;
using DiztinGUIsh.window;

namespace DiztinGUIsh
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Junk();
            // return; 

            if (Environment.OSVersion.Version.Major >= 6)
            {
                SetProcessDPIAware();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var window = new MainWindow();

            if (args.Length > 0) 
                window.OpenProject(args[0]);

            Application.Run(window);
        }

        static void Junk()
        {
            var bytes = new byte[22];

            using var pipeClient = new NamedPipeClientStream(".",
                "bsnes_tracelog", PipeDirection.In);

            // Connect to the pipe or wait until the pipe is available.
            Console.Write("Attempting to connect to pipe...");
            pipeClient.Connect();

            Console.WriteLine("Connected to pipe.");
            Console.WriteLine("There are currently {0} pipe server instances open.",
                pipeClient.NumberOfServerInstances);

            var bytesRead = 0;
            do
            {
                bytesRead = pipeClient.Read(bytes, 0, 22);
                Debug.Assert(bytesRead == 22);
                Console.WriteLine(bytes[0] == 0xEE ? "yep" : "nah");
            } while (bytesRead > 0);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}
