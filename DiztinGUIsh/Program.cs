using System;
using System.Windows.Forms;
using Diz.Core;
using Diz.Core.model;
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

            var data = SampleRomData.SampleData;

            var form = StartNewFormInstance(data);
            form.Show();

            // start a second copy of the form (NOT the right way to do this but fine for this demo)
            StartNewFormInstance(data).Show();

            Application.Run(form);
        }

        private static DataGridEditorForm StartNewFormInstance(Data data)
        {
            var byteViewerController = new RomByteGridFormController
            {
                Data = data,
            };
            var dataGridEditorForm = new DataGridEditorForm(byteViewerController);
            byteViewerController.View = dataGridEditorForm;
            
            return dataGridEditorForm;
        }
    }
}