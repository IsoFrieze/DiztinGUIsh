// #define DIZ_WINFORMS

using System.Diagnostics;
using System.Runtime.InteropServices;
using Diz.Controllers.interfaces;
using Eto.Forms;

namespace Diz.App.Eto;

public class DizEtoApp(IViewFactory viewFactory) : IDizApp
{
    #if DIZ_WINFORMS
    //hack
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();
    #endif
    
    public void Run(string initialProjectFileToOpen = "")
    {
        var application = new Application();

        #if DIZ_WINFORMS
        // hack
        if (application.Platform.IsWinForms)
        {
            if (Environment.OSVersion.Version.Major >= 6) {
                SetProcessDPIAware();
            }
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        }
        // end hack
        #endif
        
        var mainWindow = viewFactory.GetMainGridWindowView();

        // janky casting
        // ReSharper disable once SuspiciousTypeConversion.Global
        var window = mainWindow as Form;
        Debug.Assert(window != null);
        
        application.Run(window);
    }
}