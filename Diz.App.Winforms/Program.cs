#nullable enable

// #define DEBUG_EXTRA_CRASH_HANDLING // for catching really stubborn crashes, like in databinding

using System;
using System.Collections.Generic;
#if DEBUG_EXTRA_CRASH_HANDLING
using System.IO;
using System.Windows.Forms;
#endif
using Diz.App.Common;
using Diz.Ui.Winforms.util;

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
        
        // --extraTitleBar "<text>": append free-form text to the main window title bar (dev QoL,
        // to tell multiple running builds/worktrees apart). Consumed here and stripped from args so
        // it isn't mistaken for a project file to open (see MainWindowTitleExtras).
        args = ExtractExtraTitleBarArg(args, out var extraTitleBar);
        MainWindowTitleExtras.CommandLineText = extraTitleBar;

        // PHASE 0 SPIKE HOOK -- throwaway, delete with the spike/ folder.
        // Runs only when explicitly asked for, so normal startup is unchanged.
        if (Array.IndexOf(args, "--avalonia-spike") >= 0)
            Diz.Spike.Avalonia.SpikeHost.Arm();

        // new-ui plan step 5: DIZ_LABEL_EDITOR=avalonia selects the Avalonia label editor.
        // Warm up Avalonia on first idle (never before SetupDpiStuff -- Phase 0 constraint);
        // the view itself would also lazy-initialize on first Show as a safety net.
        if (LabelEditorBackend.FromEnvironment() == LabelEditorBackendKind.Avalonia)
        {
            LabelEditorBackend.ArmAvaloniaPreInitOnFirstIdle();

            // "Live typing bug" fix: the shared WinForms message loop drops WM_CHAR destined for
            // Avalonia's (foreign) top-level windows, killing all typing while key handling still
            // works. This filter re-pumps keyboard messages for Avalonia HWNDs. See
            // AvaloniaKeyboardMessageFilter for the full evidence trail.
            System.Windows.Forms.Application.AddMessageFilter(new AvaloniaKeyboardMessageFilter());
        }

        var serviceFactory = DizWinformsRegisterServices.CreateServiceFactoryAndRegisterTypes();
        DizAppCommon.StartApp(serviceFactory, args);
    }

    // Pull "--extraTitleBar <text>" (or "--extraTitleBar=<text>") out of args, returning the remaining
    // args untouched so downstream arg handling (e.g. a project file to open) is unaffected.
    private static string[] ExtractExtraTitleBarArg(string[] args, out string? value)
    {
        value = null;
        const string flag = "--extraTitleBar";
        const string inlineFlag = flag + "=";

        var remaining = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals(flag, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                    value = args[++i]; // consume the following token as the value
                continue;
            }
            if (arg.StartsWith(inlineFlag, StringComparison.OrdinalIgnoreCase))
            {
                value = arg.Substring(inlineFlag.Length);
                continue;
            }
            remaining.Add(arg);
        }
        return remaining.ToArray();
    }
}