using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using WinFormsApp = System.Windows.Forms.Application;

namespace Diz.Spike.Avalonia;

/// <summary>
/// PHASE 0 SPIKE ENTRY POINT. Throwaway.
///
/// Bootstraps Avalonia 12.1.0 inside the already-running WinForms process and
/// exercises both hosting models against the real CT label corpus.
/// </summary>
public static class SpikeHost
{
    private static bool avaloniaInitialized;
    private static List<LabelRow> rows;

    /// <summary>Set DIZ_SPIKE_AUTO=1 to run the whole battery unattended and exit.</summary>
    private static bool AutoMode =>
        Environment.GetEnvironmentVariable("DIZ_SPIKE_AUTO") == "1";

    /// <summary>
    /// Called from Diz.App.Winforms.Program BEFORE Application.Run. We only
    /// arm a timer here; the actual work happens once the WinForms message loop
    /// is pumping, which is what we are trying to prove coexistence with.
    /// </summary>
    public static void Arm()
    {
        SpikeLog.Write("SpikeHost.Arm() called from Diz.App.Winforms.Program");
        SpikeLog.Write($"AutoMode={AutoMode}");

        // GOTCHA (observed): we must NOT construct any WinForms object here.
        // Diz calls Application.SetCompatibleTextRenderingDefault() later, inside
        // DizWinformsApp.Run -> WinformsGuiUtil.SetupDpiStuff(), and that throws
        // "must be called before the first IWin32Window object is created" if the
        // spike has already created e.g. a System.Windows.Forms.Timer.
        // Application.Idle creates no window handle, so it is safe to subscribe now
        // and it first fires once the message loop is actually running.
        WinFormsApp.Idle += OnFirstIdle;
    }

    private static bool started;

    private static void OnFirstIdle(object sender, EventArgs e)
    {
        if (started)
            return;
        started = true;
        WinFormsApp.Idle -= OnFirstIdle;

        SpikeLog.Write("first Application.Idle reached -- WinForms message loop is running, starting spike");

        try
        {
            Run();
        }
        catch (Exception ex)
        {
            SpikeLog.Error("SpikeHost.Run", ex);
        }

        if (AutoMode)
        {
            SpikeLog.Write("AutoMode: requesting WinForms Application.Exit()");
            WinFormsApp.Exit();
        }
    }

    private static void Run()
    {
        rows = LabelLoader.Load();

        InitAvalonia();
        ProbeDispatcher();

        var winA = TestModelA_TopLevelWindow();
        var formB = TestModelB_EmbeddedInWinForms();

        TestWinFormsResponsivenessWhileAvaloniaOpen();
        CaptureDizMainWindow();
        TestRepeatedOpenClose();

        // leave them open in interactive mode so a human can look at them
        if (AutoMode)
        {
            try { winA?.Close(); } catch (Exception ex) { SpikeLog.Error("close winA", ex); }
            try { formB?.Close(); } catch (Exception ex) { SpikeLog.Error("close formB", ex); }
        }

        SpikeLog.Write("=== SPIKE COMPLETE ===");
    }

    // ---------------------------------------------------------------- bootstrap

    private static void InitAvalonia()
    {
        if (avaloniaInitialized)
            return;

        var sw = Stopwatch.StartNew();

        // THE BOOTSTRAP. SetupWithoutStarting() is the load-bearing call: it builds
        // the Avalonia platform + dispatcher but never enters Avalonia's own message
        // loop, because WinForms Application.Run already owns this thread's loop.
        AppBuilder.Configure<SpikeApp>()
            .UsePlatformDetect()
            .LogToTrace()
            .SetupWithoutStarting();

        sw.Stop();
        avaloniaInitialized = true;

        SpikeLog.Write($"Avalonia SetupWithoutStarting() OK in {sw.ElapsedMilliseconds} ms");
        SpikeLog.Write($"  Avalonia assembly version: {typeof(AppBuilder).Assembly.GetName().Version}");
        var currentAppName = global::Avalonia.Application.Current?.GetType().FullName ?? "<null>";
        SpikeLog.Write($"  Avalonia Application.Current: {currentAppName}");
        SpikeLog.Write($"  WinForms Application.MessageLoop = {WinFormsApp.MessageLoop}");
    }

    private static void ProbeDispatcher()
    {
        var d = Dispatcher.UIThread;
        SpikeLog.Write($"Dispatcher.UIThread.CheckAccess() from WinForms UI thread = {d.CheckAccess()}");

        var posted = false;
        d.Post(() =>
        {
            posted = true;
            SpikeLog.Write("Dispatcher.UIThread.Post() callback RAN (proves Avalonia dispatcher is pumped by the WinForms loop)");
        });

        // Give the WinForms loop a chance to pump the posted Avalonia job.
        WinFormsApp.DoEvents();
        SpikeLog.Write($"after DoEvents(), avalonia posted-job executed = {posted}");
    }

    // ---------------------------------------------------------------- model (a)

    private static SpikeAvaloniaWindow TestModelA_TopLevelWindow()
    {
        SpikeLog.Write("--- MODEL (a): Avalonia top-level Window ---");
        try
        {
            var sw = Stopwatch.StartNew();
            var win = new SpikeAvaloniaWindow(rows);
            win.Show();
            sw.Stop();
            SpikeLog.Write($"model (a) Show() OK in {sw.ElapsedMilliseconds} ms");

            PumpBoth(400);

            win.Virtualizing.ListBoxControl.UpdateLayout();
            SpikeLog.Write($"model (a) virtualization: {win.Virtualizing.DescribeVirtualization()}");
            win.Virtualizing.ScrollStressTest();
            SpikeLog.Write("model (a) filter stress test:");
            win.Virtualizing.FilterStressTest();

            // Control group: the non-virtualizing version. NOTE: the TabItem's
            // content is not realized until its tab is actually selected, so we
            // must select it first or the measurement silently reports nothing.
            SpikeLog.Write("selecting 'Naive ItemsControl' tab to force realization...");
            var naiveSw = Stopwatch.StartNew();
            win.Tabs.SelectedIndex = 1;
            PumpBoth(1500);
            win.Naive.MeasureRealization();
            naiveSw.Stop();
            SpikeLog.Write($"naive tab total realize+layout wall time: {naiveSw.ElapsedMilliseconds} ms");
            win.Tabs.SelectedIndex = 0;
            PumpBoth(300);

            LogDpiAndTheme(win);
            CaptureAvaloniaVisual(win, "model_a_toplevel.png");
            CaptureScreenRegion(win.Position.X, win.Position.Y, (int)win.Width, (int)win.Height, "model_a_screen.png");

            return win;
        }
        catch (Exception ex)
        {
            SpikeLog.Error("MODEL (a)", ex);
            return null;
        }
    }

    // ---------------------------------------------------------------- model (b)

    private static SpikeEmbeddedForm TestModelB_EmbeddedInWinForms()
    {
        SpikeLog.Write("--- MODEL (b): Avalonia embedded in a WinForms Form ---");
        try
        {
            var sw = Stopwatch.StartNew();
            var form = new SpikeEmbeddedForm(rows);
            form.Show();
            sw.Stop();
            SpikeLog.Write($"model (b) Show() OK in {sw.ElapsedMilliseconds} ms");

            PumpBoth(400);

            form.View.ListBoxControl.UpdateLayout();
            SpikeLog.Write($"model (b) virtualization: {form.View.DescribeVirtualization()}");
            form.View.ScrollStressTest();

            var b = form.Bounds;
            CaptureScreenRegion(b.X, b.Y, b.Width, b.Height, "model_b_screen.png");

            return form;
        }
        catch (Exception ex)
        {
            SpikeLog.Error("MODEL (b)", ex);
            return null;
        }
    }

    // ---------------------------------------------------------------- coexistence

    private static void TestWinFormsResponsivenessWhileAvaloniaOpen()
    {
        SpikeLog.Write("--- WinForms responsiveness while Avalonia windows are open ---");
        try
        {
            const int iterations = 200;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
                WinFormsApp.DoEvents();
            sw.Stop();
            SpikeLog.Write($"{iterations}x Application.DoEvents() with Avalonia live: " +
                           $"{sw.Elapsed.TotalMilliseconds:F1} ms total, {sw.Elapsed.TotalMilliseconds / iterations:F3} ms each");

            // A WinForms timer must still fire while Avalonia is up.
            var fired = 0;
            var t = new System.Windows.Forms.Timer { Interval = 15 };
            t.Tick += (_, _) => fired++;
            t.Start();
            var deadline = Stopwatch.StartNew();
            while (deadline.ElapsedMilliseconds < 500)
                WinFormsApp.DoEvents();
            t.Stop();
            t.Dispose();
            SpikeLog.Write($"WinForms Timer fired {fired} times in 500 ms while Avalonia windows open " +
                           $"(expect roughly 30; 0 would mean the WinForms loop is starved)");
        }
        catch (Exception ex)
        {
            SpikeLog.Error("responsiveness test", ex);
        }
    }

    private static void TestRepeatedOpenClose()
    {
        SpikeLog.Write("--- repeated open/close leak + deadlock test (model a) ---");
        try
        {
            const int cycles = 25;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var proc = Process.GetCurrentProcess();
            proc.Refresh();
            var memBefore = GC.GetTotalMemory(true);
            var handlesBefore = proc.HandleCount;
            var gdiBefore = GetGuiResources(proc.Handle, 0);
            var userBefore = GetGuiResources(proc.Handle, 1);

            var sw = Stopwatch.StartNew();
            for (var i = 0; i < cycles; i++)
            {
                var w = new SpikeAvaloniaWindow(rows);
                w.Show();
                PumpBoth(20);
                w.Close();
                PumpBoth(20);
            }
            sw.Stop();
            SpikeLog.Write($"{cycles} open/close cycles completed WITHOUT deadlock in {sw.ElapsedMilliseconds} ms " +
                           $"({sw.ElapsedMilliseconds / (double)cycles:F1} ms/cycle)");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            proc.Refresh();
            var memAfter = GC.GetTotalMemory(true);
            var handlesAfter = proc.HandleCount;
            var gdiAfter = GetGuiResources(proc.Handle, 0);
            var userAfter = GetGuiResources(proc.Handle, 1);

            SpikeLog.Write($"  managed heap : {memBefore:N0} -> {memAfter:N0} (delta {memAfter - memBefore:N0} bytes, " +
                           $"{(memAfter - memBefore) / (double)cycles:N0}/cycle)");
            SpikeLog.Write($"  OS handles   : {handlesBefore} -> {handlesAfter} (delta {handlesAfter - handlesBefore})");
            SpikeLog.Write($"  GDI objects  : {gdiBefore} -> {gdiAfter} (delta {gdiAfter - gdiBefore})");
            SpikeLog.Write($"  USER objects : {userBefore} -> {userAfter} (delta {userAfter - userBefore})");
        }
        catch (Exception ex)
        {
            SpikeLog.Error("open/close test", ex);
        }
    }

    /// <summary>
    /// Regression check: bring Diz's OWN WinForms main window to the front while
    /// Avalonia is live and screenshot it, so we can see whether hosting Avalonia
    /// damaged the existing WinForms UI.
    /// </summary>
    private static void CaptureDizMainWindow()
    {
        try
        {
            SpikeLog.Write("--- WinForms regression check: Diz main window ---");
            foreach (Form f in WinFormsApp.OpenForms)
                SpikeLog.Write($"  open WinForms form: {f.GetType().FullName} '{f.Text}' bounds={f.Bounds} visible={f.Visible}");

            var main = WinFormsApp.OpenForms
                .Cast<Form>()
                .FirstOrDefault(f => f is not SpikeEmbeddedForm && f.Visible);

            if (main == null)
            {
                SpikeLog.Write("  no Diz WinForms form found to capture");
                return;
            }

            main.BringToFront();
            main.Activate();
            PumpBoth(600);
            var b = main.Bounds;
            CaptureScreenRegion(b.X, b.Y, b.Width, b.Height, "winforms_main_after_avalonia.png");
        }
        catch (Exception ex)
        {
            SpikeLog.Error("CaptureDizMainWindow", ex);
        }
    }

    // ---------------------------------------------------------------- dpi/theme

    private static void LogDpiAndTheme(global::Avalonia.Controls.Window win)
    {
        try
        {
            var scaling = win.RenderScaling;
            SpikeLog.Write($"DPI: Avalonia Window.RenderScaling = {scaling}");
            SpikeLog.Write($"DPI: WinForms Screen.PrimaryScreen.Bounds = {Screen.PrimaryScreen?.Bounds}");
            using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                SpikeLog.Write($"DPI: desktop DpiX={g.DpiX} DpiY={g.DpiY} (=> scale {g.DpiX / 96.0})");
            SpikeLog.Write($"DPI: WinForms Application high-dpi mode set? see app.manifest/ApplicationConfiguration");
            SpikeLog.Write($"THEME: Avalonia ActualThemeVariant = {win.ActualThemeVariant}");
            SpikeLog.Write($"THEME: SystemColors.Window = {System.Drawing.SystemColors.Window} (WinForms side)");
        }
        catch (Exception ex)
        {
            SpikeLog.Error("dpi/theme probe", ex);
        }
    }

    // ---------------------------------------------------------------- evidence

    /// <summary>
    /// Render the live Avalonia visual tree to a PNG. This proves the Avalonia
    /// RENDER PIPELINE produces real pixels; it does NOT prove on-screen
    /// composition (see CaptureScreenRegion for that).
    /// </summary>
    private static void CaptureAvaloniaVisual(global::Avalonia.Controls.Window win, string fileName)
    {
        try
        {
            var w = (int)Math.Max(1, win.Bounds.Width);
            var h = (int)Math.Max(1, win.Bounds.Height);
            var path = Path.Combine(SpikeLog.ArtifactDir, fileName);

            using var rtb = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
            rtb.Render(win);
            using var fs = File.Create(path);
            rtb.Save(fs);

            SpikeLog.Write($"EVIDENCE: rendered Avalonia visual tree to {path} ({w}x{h})");
        }
        catch (Exception ex)
        {
            SpikeLog.Error($"CaptureAvaloniaVisual({fileName})", ex);
        }
    }

    /// <summary>
    /// Real desktop screen grab of a window's rectangle. Only meaningful in an
    /// interactive session with an unlocked desktop.
    /// </summary>
    private static void CaptureScreenRegion(int x, int y, int w, int h, string fileName)
    {
        try
        {
            if (w <= 0 || h <= 0)
            {
                SpikeLog.Write($"EVIDENCE: skipping {fileName}, bad bounds {w}x{h}");
                return;
            }

            var path = Path.Combine(SpikeLog.ArtifactDir, fileName);
            using var bmp = new System.Drawing.Bitmap(w, h);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h));
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);

            // crude all-one-color check => nothing actually painted there
            var distinct = new HashSet<int>();
            for (var yy = 0; yy < h; yy += 7)
            for (var xx = 0; xx < w; xx += 7)
                distinct.Add(bmp.GetPixel(xx, yy).ToArgb());

            SpikeLog.Write($"EVIDENCE: screen-captured {path} ({w}x{h}), {distinct.Count} distinct sampled colors " +
                           $"({(distinct.Count <= 2 ? "SUSPICIOUS - looks blank/occluded" : "looks like real content")})");
        }
        catch (Exception ex)
        {
            SpikeLog.Error($"CaptureScreenRegion({fileName})", ex);
        }
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Pump BOTH message loops for a while.</summary>
    private static void PumpBoth(int ms)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            WinFormsApp.DoEvents();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetGuiResources(IntPtr hProcess, int uiFlags);
}
