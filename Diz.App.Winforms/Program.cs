#nullable enable

// #define DEBUG_EXTRA_CRASH_HANDLING // for catching really stubborn crashes, like in databinding

using System;
#if DEBUG_EXTRA_CRASH_HANDLING
using System.IO;
using System.Windows.Forms;
#endif
using Diz.App.Common;

namespace Diz.App.Winforms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        #if DEBUG_EXTRA_CRASH_HANDLING
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); // dangerous
        Application.ThreadException += (sender, e) => 
        {
            File.WriteAllText("crash_log.txt", $"Thread Exception: {e.Exception}");
            MessageBox.Show($"Thread Exception: {e.Exception}");
        };
    
        AppDomain.CurrentDomain.UnhandledException += (sender, e) => 
        {
            File.WriteAllText("crash_log.txt", $"Unhandled Exception: {e.ExceptionObject}");
            MessageBox.Show($"Unhandled Exception: {e.ExceptionObject}");
        };
        #endif
        
        var serviceFactory = DizWinformsRegisterServices.CreateServiceFactoryAndRegisterTypes();
        DizAppCommon.StartApp(serviceFactory, args);
    }
}