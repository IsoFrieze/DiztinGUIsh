using System;
using System.Windows.Forms;
using Diz.Core;
using DiztinGUIsh.util;
using DiztinGUIsh.window;
using DiztinGUIsh.window2;

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
            
            // Application.Run(new DizApplication(openFile));
            
            // this is the one eventually we want
            /*var startForm = new StartForm();
            startForm.Show();
            Application.Run(startForm);*/

            
            // run this as a test
            var f = new DataGridEditorTest();
            f.LoadData(SampleRomData.SampleData.RomBytes);
            f.Show();
            Application.Run(f);
        }
    }
}