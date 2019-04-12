using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainWindow window = new MainWindow();
            Application.Run(window);
            if (args.Length > 0 && Project.TryOpenProject(args[0]))
            {
                window.TriggerSaveOptions(true, true);
                window.UpdateWindowTitle();
            }
        }
    }
}
