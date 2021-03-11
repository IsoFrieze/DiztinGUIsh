using System;
using System.Windows.Forms;
using Diz.Core;
using DiztinGUIsh.util;
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
            
            var form = StartNewFormInstance();
            
            // start a second copy of the form (NOT the right way to do this but hey it works for now)
            StartNewFormInstance();

            Application.Run(form);
        }

        private static DataGridEditorForm StartNewFormInstance()
        {
            var bytesViewerController = new BytesViewerController();
            bytesViewerController.CreateDataBindingTo(SampleRomData.SampleData);
            var form = new DataGridEditorForm(bytesViewerController);
            form.Show();
            
            return form;
        }
    }
}