// Headless render + interaction harness for the Avalonia label editor.
// See docs/diz/new-ui-plan.md, "Preview harness (headless iteration loop)".
//
// NAMESPACE NOTE: this file lives in `DizPreview`, deliberately NOT under Diz.Ui.Avalonia.*,
// so the bare identifier `Avalonia` keeps resolving to the framework (the collision gotcha
// documented in Diz.Ui.Avalonia/DizAvaloniaApp.cs). Friend access to the internal
// LabelEditorWindow comes from the InternalsVisibleTo("Diz.Ui.Avalonia.Preview") grant in
// Diz.Ui.Avalonia.csproj, which matches on ASSEMBLY name, not namespace.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Diz.Core.Interfaces;
using Diz.Ui.Avalonia;             // DizAvaloniaApp, AvaloniaLabelEditorView, AvaloniaFileDialogService, LabelEditorWindow (internal)
using Diz.Ui.ViewModels.Labels;    // LabelEditorViewModel, ILabelEditorViewModel, ILabelRowViewModel, LabelField

namespace DizPreview;

internal static class Program
{
    private static int Main(string[] args)
    {
        var outDir = ParseOut(args);
        Directory.CreateDirectory(outDir);

        Console.WriteLine("=== Avalonia label-editor headless preview harness ===");
        Console.WriteLine($"out dir : {Path.GetFullPath(outDir)}");
        Console.WriteLine($"fixture : {PreviewFixture.Count} labels (no project, no ROM, no Data)");
        Console.WriteLine();

        // Real Skia rendering into offscreen frames: UseHeadlessDrawing=false is what makes
        // CaptureRenderedFrame return actual pixels instead of a stub. We reuse DizAvaloniaApp
        // (the real app's Application object) so the Fluent LIGHT theme is exactly the app's.
        AppBuilder.Configure<DizAvaloniaApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        // Build the editor over the dummy provider. This is the same composition
        // AvaloniaLabelEditorView.RecreateViewModel does, minus the project ports.
        var provider = PreviewFixture.Build();
        var vm = new LabelEditorViewModel(provider, notificationMarshaller: RunOnUiThread);

        // The window ctor requires an AvaloniaLabelEditorView host (for its toolbar commands).
        // We give it a real one with a real file-dialog service; the project controller stays
        // null, so only project-dependent toolbar buttons would fail -- and we never click them.
        var host = new AvaloniaLabelEditorView(new AvaloniaFileDialogService());
        var window = new LabelEditorWindow(host);
        window.AttachViewModel(vm);

        window.Show();
        Pump();

        // ------------------------------------------------------------------ RENDER STATES
        Capture(window, Path.Combine(outDir, "default.png"), "default (unfiltered, nothing selected)");

        // a row WITH alternate contexts selected, so the details pane shows the contexts editor
        var ctxRow = RowByAddress(vm, 0x7E1422); // scratch_tbl44: battle + menu overrides
        vm.SelectedRow = ctxRow;
        Pump();
        Capture(window, Path.Combine(outDir, "row-selected.png"),
            $"row selected: {(ctxRow == null ? "<none>" : ctxRow.AddressText + " " + ctxRow.Name)} (details pane populated)");

        // search filtered to a handful
        vm.SelectedRow = null;
        vm.SearchTerm = "party";
        Pump();
        Capture(window, Path.Combine(outDir, "search-filtered.png"),
            $"search 'party' -> {vm.VisibleLabelCount}/{vm.TotalLabelCount} visible");
        vm.SearchTerm = "";
        Pump();

        // narrow window
        window.Width = 760;
        window.Height = 500;
        Pump();
        Capture(window, Path.Combine(outDir, "narrow.png"), "narrow 760x500");

        // restore for the interaction probe
        window.Width = 1050;
        window.Height = 640;
        // scroll back to top: select first row then clear
        if (vm.Rows.Count > 0)
            vm.SelectedRow = vm.Rows[0];
        vm.SelectedRow = null;
        Pump();

        // ------------------------------------------------------------------ INTERACTION PROBE
        Console.WriteLine();
        Console.WriteLine("================= INTERACTION PROBE =================");
        Console.WriteLine("Gestures are simulated via Avalonia.Headless (real mouse/key events).");
        Console.WriteLine("HARNESS-FAIL = harness couldn't perform the gesture (bad coords etc).");
        Console.WriteLine("APP-FAIL     = gesture performed, but the app ignored it (a real bug).");
        Console.WriteLine();

        var report = new List<string>();
        ProbeSearchBox(window, vm, report);
        ProbeCell(window, vm, provider, report, LabelField.Name, targetAddress: 0x7E1401,
            newValue: "probe_name_edit");
        ProbeCell(window, vm, provider, report, LabelField.Comment, targetAddress: 0x7E1403,
            newValue: "probe comment edit");
        ProbeCell(window, vm, provider, report, LabelField.Address, targetAddress: 0x7E1405,
            newValue: "7E1FFF");

        Pump();
        Capture(window, Path.Combine(outDir, "after-typing.png"), "after grid-cell interaction probe");

        // --------------------------------------------------------------- DETAILS-PANE PROBES
        vm.SelectedRow = null;
        Pump();
        ProbeDetailsName(window, vm, provider, report, targetAddress: 0x7E1406,
            newValue: "details_name_edit");
        ProbeDetailsComment(window, vm, provider, report, targetAddress: 0x7E1407,
            newValue: "details comment edit\nsecond line");
        Pump();
        Capture(window, Path.Combine(outDir, "details-comment-edited.png"),
            "details pane: name + multi-line comment edited via the right-side pane");

        // --------------------------------------------------------------- CONTEXT-EDITOR PROBES
        ProbeContextAdd(window, vm, provider, report, targetAddress: 0x7E1600,
            context: "menu", nameOverride: "menu_foo");
        ProbeContextEditExisting(window, vm, provider, report, targetAddress: 0x7E1420,
            existingContext: "battle", newOverride: "battle_override_edited");
        Pump();
        Capture(window, Path.Combine(outDir, "context-added.png"),
            "details pane: alternate-context mapping added + an existing override edited");

        // ------------------------------------------------------------------ PROGRESS POPUP (step 6 Part C)
        // The Avalonia progress window in both modes: marquee (open/save/export) and determinate
        // (trace-log import reports bytes-read %). Rendered here so the popup can be reviewed as
        // PNGs alongside the label editor.
        CaptureProgressPopup(outDir);

        Console.WriteLine();
        Console.WriteLine("================= PROBE SUMMARY =================");
        foreach (var line in report)
            Console.WriteLine(line);
        Console.WriteLine("================================================");

        return 0;
    }

    // ------------------------------------------------------------------ progress popup (step 6 Part C)

    private static void CaptureProgressPopup(string outDir)
    {
        Console.WriteLine();
        Console.WriteLine("[progress popup]");

        var marquee = new ProgressWindow();
        marquee.SetDescription("Exporting assembly source code...");
        marquee.SetMarquee(true);
        marquee.Show();
        Pump();
        Capture(marquee, Path.Combine(outDir, "progress-marquee.png"),
            "progress popup: marquee (indeterminate) -- open/save/export");
        marquee.Close();

        var determinate = new ProgressWindow();
        determinate.SetDescription("Importing trace logs...");
        determinate.SetMarquee(false);
        determinate.SetProgress(60);
        determinate.Show();
        Pump();
        Capture(determinate, Path.Combine(outDir, "progress-determinate.png"),
            "progress popup: determinate 60% -- trace-log import");
        determinate.Close();
        Pump();
    }

    // ------------------------------------------------------------------ probes

    private static void ProbeSearchBox(LabelEditorWindow window, ILabelEditorViewModel vm, List<string> report)
    {
        Console.WriteLine("[search box]");
        try
        {
            var box = FindSearchBox(window);
            if (box == null) { Record(report, "search-box", "HARNESS-FAIL", "could not find the search TextBox in the visual tree"); return; }

            var pt = CenterInWindow(box, window);
            if (pt == null) { Record(report, "search-box", "HARNESS-FAIL", "search box has no measurable on-screen position (not laid out)"); return; }

            Click(window, pt.Value);
            var focused = window.FocusManager?.GetFocusedElement();
            var focusOk = ReferenceEquals(focused, box);
            Console.WriteLine($"  clicked @ {pt.Value}; focused == searchBox: {focusOk} (focused={Describe(focused)})");

            var before = vm.VisibleLabelCount;
            window.KeyTextInput("party");
            Pump();
            var textOk = (box.Text ?? "") == "party";
            var vmOk = vm.SearchTerm == "party" && vm.VisibleLabelCount != before && vm.VisibleLabelCount < vm.TotalLabelCount;
            Console.WriteLine($"  typed 'party'; box.Text='{box.Text}'; vm.SearchTerm='{vm.SearchTerm}'; visible {before}->{vm.VisibleLabelCount}/{vm.TotalLabelCount}");

            // reset filter for the row probes
            vm.SearchTerm = "";
            Pump();

            if (!focusOk) Record(report, "search-box focus", "APP-FAIL", $"click did not focus the search box (focused={Describe(focused)})");
            else Record(report, "search-box focus", "PASS", "click focused the search box");

            if (textOk) Record(report, "search-box typing", "PASS", "typed text appeared in the box");
            else Record(report, "search-box typing", "APP-FAIL", $"typed 'party' but box.Text='{box.Text}'");

            if (vmOk) Record(report, "search-box -> vm filter", "PASS", $"vm filtered to {vm.TotalLabelCount}->fewer rows");
            else Record(report, "search-box -> vm filter", "APP-FAIL", $"vm did not filter (SearchTerm='{vm.SearchTerm}')");
        }
        catch (Exception ex)
        {
            Record(report, "search-box", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void ProbeCell(
        LabelEditorWindow window, ILabelEditorViewModel vm, ILabelProvider provider,
        List<string> report, LabelField field, int targetAddress, string newValue)
    {
        var label = field.ToString().ToLowerInvariant();
        Console.WriteLine($"[{label} cell @ {targetAddress:X6}]");
        try
        {
            var cell = FindRowCell(window, targetAddress, field);
            if (cell == null) { Record(report, $"{label} cell", "HARNESS-FAIL", $"could not find the {label} TextBox for row {targetAddress:X6} (row not realized?)"); return; }

            var pt = CenterInWindow(cell, window);
            if (pt == null) { Record(report, $"{label} cell", "HARNESS-FAIL", $"{label} cell has no measurable position"); return; }

            var originalName = provider.GetLabel(targetAddress)?.Name;
            var originalComment = provider.GetLabel(targetAddress)?.Comment;

            Click(window, pt.Value);
            var focused = window.FocusManager?.GetFocusedElement();
            var focusOk = ReferenceEquals(focused, cell);
            Console.WriteLine($"  clicked @ {pt.Value}; focused == cell: {focusOk} (focused={Describe(focused)})");

            // select-all then type, mimicking a user replacing the field
            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            Pump();
            window.KeyTextInput(newValue);
            Pump();
            var textOk = (cell.Text ?? "") == newValue;
            Console.WriteLine($"  typed '{newValue}'; cell.Text='{cell.Text}'");

            // commit with Enter
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Pump();

            bool commitOk;
            string commitDetail;
            switch (field)
            {
                case LabelField.Name:
                    var newName = provider.GetLabel(targetAddress)?.Name;
                    commitOk = newName == newValue;
                    commitDetail = $"provider name '{originalName}' -> '{newName}' (wanted '{newValue}')";
                    break;
                case LabelField.Comment:
                    var newComment = provider.GetLabel(targetAddress)?.Comment;
                    commitOk = newComment == newValue;
                    commitDetail = $"provider comment '{originalComment}' -> '{newComment}' (wanted '{newValue}')";
                    break;
                case LabelField.Address:
                default:
                    var parsed = int.Parse(newValue, System.Globalization.NumberStyles.HexNumber);
                    var movedThere = provider.GetLabel(parsed) != null;
                    var goneFromOld = provider.GetLabel(targetAddress) == null;
                    commitOk = movedThere && goneFromOld;
                    commitDetail = $"label at {parsed:X6} exists: {movedThere}; gone from {targetAddress:X6}: {goneFromOld}";
                    break;
            }
            Console.WriteLine($"  commit: {commitDetail}; vm.StatusText='{vm.StatusText}'");

            if (focusOk) Record(report, $"{label} cell focus", "PASS", "click focused the cell");
            else Record(report, $"{label} cell focus", "APP-FAIL", $"click did not focus the cell (focused={Describe(focused)})");

            if (textOk) Record(report, $"{label} cell typing", "PASS", "typed text appeared in the cell");
            else Record(report, $"{label} cell typing", "APP-FAIL", $"typed '{newValue}' but cell.Text='{cell.Text}'");

            if (commitOk) Record(report, $"{label} cell commit", "PASS", commitDetail);
            else Record(report, $"{label} cell commit", "APP-FAIL", commitDetail + $" | status='{vm.StatusText}'");
        }
        catch (Exception ex)
        {
            Record(report, $"{label} cell", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ details-pane probes

    private static void ProbeDetailsName(
        LabelEditorWindow window, ILabelEditorViewModel vm, ILabelProvider provider,
        List<string> report, int targetAddress, string newValue)
    {
        Console.WriteLine($"[details-pane NAME @ {targetAddress:X6}]");
        try
        {
            vm.SelectedRow = RowByAddress(vm, targetAddress);
            Pump();

            var box = FindDetailsBox(window, "details-name");
            if (box == null) { Record(report, "details name", "HARNESS-FAIL", "details Name TextBox not found"); return; }

            var pt = CenterInWindow(box, window);
            if (pt == null) { Record(report, "details name", "HARNESS-FAIL", "details Name box has no on-screen position"); return; }

            var before = provider.GetLabel(targetAddress)?.Name;
            Click(window, pt.Value);
            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            Pump();
            window.KeyTextInput(newValue);
            Pump();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Pump();

            var after = provider.GetLabel(targetAddress)?.Name;
            Console.WriteLine($"  provider name '{before}' -> '{after}' (wanted '{newValue}')");
            if (after == newValue)
                Record(report, "details name -> provider", "PASS", $"'{before}' -> '{after}'");
            else
                Record(report, "details name -> provider", "APP-FAIL", $"'{before}' -> '{after}' (wanted '{newValue}')");
        }
        catch (Exception ex)
        {
            Record(report, "details name", "EXCEPTION", ex.GetType().Name + ": " + (ex.InnerException?.Message ?? ex.Message));
        }
    }

    private static void ProbeDetailsComment(
        LabelEditorWindow window, ILabelEditorViewModel vm, ILabelProvider provider,
        List<string> report, int targetAddress, string newValue)
    {
        Console.WriteLine($"[details-pane COMMENT @ {targetAddress:X6}]");
        try
        {
            vm.SelectedRow = RowByAddress(vm, targetAddress);
            Pump();

            var box = FindDetailsBox(window, "details-comment");
            if (box == null) { Record(report, "details comment", "HARNESS-FAIL", "details Comment TextBox not found"); return; }

            var pt = CenterInWindow(box, window);
            if (pt == null) { Record(report, "details comment", "HARNESS-FAIL", "details Comment box has no on-screen position"); return; }

            var before = provider.GetLabel(targetAddress)?.Comment;
            Click(window, pt.Value);
            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            Pump();
            window.KeyTextInput(newValue);
            Pump();

            // Comment commits on LostFocus (Enter inserts a newline). Move focus to the Name box.
            var nameBox = FindDetailsBox(window, "details-name");
            var np = nameBox == null ? null : CenterInWindow(nameBox, window);
            if (np != null) Click(window, np.Value);
            Pump();

            var after = provider.GetLabel(targetAddress)?.Comment;
            Console.WriteLine($"  provider comment '{before}' -> '{after}' (wanted '{newValue}')");
            if (after == newValue)
                Record(report, "details comment -> provider", "PASS", $"comment committed via LostFocus");
            else
                Record(report, "details comment -> provider", "APP-FAIL", $"'{before}' -> '{after}' (wanted '{newValue}')");
        }
        catch (Exception ex)
        {
            Record(report, "details comment", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void ProbeContextAdd(
        LabelEditorWindow window, ILabelEditorViewModel vm, ILabelProvider provider,
        List<string> report, int targetAddress, string context, string nameOverride)
    {
        Console.WriteLine($"[context-editor ADD @ {targetAddress:X6}]");
        try
        {
            vm.SelectedRow = RowByAddress(vm, targetAddress);
            Pump();

            var before = provider.GetLabel(targetAddress)?.ContextMappings.Count ?? -1;

            var addBtn = FindAddContextButton(window);
            if (addBtn == null) { Record(report, "context add", "HARNESS-FAIL", "'+ Add' button not found"); return; }
            var abp = CenterInWindow(addBtn, window);
            if (abp == null) { Record(report, "context add", "HARNESS-FAIL", "'+ Add' button has no position"); return; }
            Click(window, abp.Value);
            Pump();

            // the new (empty) mapping is the last one on the label. Type into its two cells.
            var ctxBox = FindContextCell(window, "ctx-context", m => string.IsNullOrEmpty(m.Context));
            var ovrBox = FindContextCell(window, "ctx-override", m => string.IsNullOrEmpty(m.NameOverride) && string.IsNullOrEmpty(m.Context));
            if (ctxBox == null || ovrBox == null)
            {
                Record(report, "context add", "HARNESS-FAIL",
                    $"new context row cells not found (ctx={ctxBox != null}, ovr={ovrBox != null})");
                return;
            }

            TypeInto(window, ctxBox, context);
            TypeInto(window, ovrBox, nameOverride);
            Pump();

            var mappings = provider.GetLabel(targetAddress)?.ContextMappings;
            var found = mappings?.Any(m => m.Context == context && m.NameOverride == nameOverride) ?? false;
            Console.WriteLine($"  ContextMappings count {before} -> {mappings?.Count}; contains ('{context}','{nameOverride}'): {found}");
            if (found)
                Record(report, "context add -> model", "PASS", $"Label.ContextMappings now contains ('{context}','{nameOverride}')");
            else
                Record(report, "context add -> model", "APP-FAIL",
                    $"('{context}','{nameOverride}') not in model (count {before}->{mappings?.Count})");
        }
        catch (Exception ex)
        {
            Record(report, "context add", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void ProbeContextEditExisting(
        LabelEditorWindow window, ILabelEditorViewModel vm, ILabelProvider provider,
        List<string> report, int targetAddress, string existingContext, string newOverride)
    {
        Console.WriteLine($"[context-editor EDIT existing @ {targetAddress:X6}]");
        try
        {
            vm.SelectedRow = RowByAddress(vm, targetAddress);
            Pump();

            var mapping = provider.GetLabel(targetAddress)?.ContextMappings
                .FirstOrDefault(m => m.Context == existingContext);
            if (mapping == null) { Record(report, "context edit", "HARNESS-FAIL", $"no existing '{existingContext}' mapping"); return; }
            var before = mapping.NameOverride;

            var ovrBox = FindContextCell(window, "ctx-override", m => m.Context == existingContext);
            if (ovrBox == null) { Record(report, "context edit", "HARNESS-FAIL", $"override cell for '{existingContext}' not found"); return; }

            var pt = CenterInWindow(ovrBox, window);
            if (pt == null) { Record(report, "context edit", "HARNESS-FAIL", "override cell has no position"); return; }
            Click(window, pt.Value);
            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            Pump();
            window.KeyTextInput(newOverride);
            Pump();

            var after = provider.GetLabel(targetAddress)?.ContextMappings
                .FirstOrDefault(m => m.Context == existingContext)?.NameOverride;
            Console.WriteLine($"  '{existingContext}' override '{before}' -> '{after}' (wanted '{newOverride}')");
            if (after == newOverride)
                Record(report, "context edit -> model", "PASS", $"'{before}' -> '{after}'");
            else
                Record(report, "context edit -> model", "APP-FAIL", $"'{before}' -> '{after}' (wanted '{newOverride}')");
        }
        catch (Exception ex)
        {
            Record(report, "context edit", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void TypeInto(LabelEditorWindow window, TextBox box, string text)
    {
        var pt = CenterInWindow(box, window);
        if (pt == null)
            return;
        Click(window, pt.Value);
        window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
        window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
        Pump();
        window.KeyTextInput(text);
        Pump();
    }

    // ------------------------------------------------------------------ control lookup

    private static ILabelRowViewModel? RowByAddress(ILabelEditorViewModel vm, int snesAddress) =>
        vm.Rows.FirstOrDefault(r => r.SnesAddress == snesAddress);

    private static TextBox? FindSearchBox(LabelEditorWindow window) =>
        window.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(tb => tb.PlaceholderText == "Search...");

    // Cells are now identified by Tag (the layout is AXAML + a code-built row template; Grid
    // column indices are an implementation detail the probe no longer depends on).
    private static string CellTag(LabelField field) => field switch
    {
        LabelField.Address => "address-cell",
        LabelField.Name => "name-cell",
        _ => "comment-cell",
    };

    private static TextBox? FindRowCell(LabelEditorWindow window, int snesAddress, LabelField field) =>
        window.GetVisualDescendants().OfType<TextBox>()
            .Where(tb => tb.DataContext is ILabelRowViewModel row && row.SnesAddress == snesAddress)
            .FirstOrDefault(tb => Equals(tb.Tag, CellTag(field)));

    private static TextBox? FindDetailsBox(LabelEditorWindow window, string tag) =>
        window.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(tb => Equals(tb.Tag, tag));

    // A context-editor cell ("ctx-context" or "ctx-override") whose row wraps a mapping the
    // predicate accepts. DataContext of each cell is the IContextMappingViewModel wrapper.
    private static TextBox? FindContextCell(
        LabelEditorWindow window, string tag, Func<IContextMappingViewModel, bool> pred) =>
        window.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(tb => Equals(tb.Tag, tag)
                                  && tb.DataContext is IContextMappingViewModel m && pred(m));

    private static Button? FindAddContextButton(LabelEditorWindow window) =>
        window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => (b.Content as string) == "+ Add");

    // ------------------------------------------------------------------ headless plumbing

    private static void Pump(int ticks = 2)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(ticks);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Capture(Window window, string path, string caption)
    {
        Pump();
        WriteableBitmap? frame = window.CaptureRenderedFrame();
        if (frame == null)
        {
            Console.WriteLine($"  WARN: CaptureRenderedFrame returned null for {Path.GetFileName(path)} ({caption})");
            return;
        }
        frame.Save(path);
        var size = new FileInfo(path).Length;
        Console.WriteLine($"  wrote {Path.GetFileName(path),-22} {frame.PixelSize.Width}x{frame.PixelSize.Height}  {size,8} bytes  -- {caption}");
    }

    private static void Click(LabelEditorWindow window, Point p)
    {
        window.MouseMove(p, RawInputModifiers.None);
        window.MouseDown(p, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(p, MouseButton.Left, RawInputModifiers.None);
        Pump();
    }

    private static Point? CenterInWindow(Visual v, Visual root)
    {
        var topLeft = v.TranslatePoint(new Point(0, 0), root);
        if (topLeft == null)
            return null;
        var b = v.Bounds;
        if (b.Width <= 0 || b.Height <= 0)
            return null;
        return new Point(topLeft.Value.X + b.Width / 2, topLeft.Value.Y + b.Height / 2);
    }

    private static string Describe(object? element) => element switch
    {
        null => "null",
        TextBox tb when tb.PlaceholderText == "Search..." => "SearchBox",
        TextBox { Tag: string t } => $"TextBox({t})",
        Control c => c.GetType().Name,
        _ => element.GetType().Name,
    };

    private static void Record(List<string> report, string what, string verdict, string detail) =>
        report.Add($"  {verdict,-12} {what,-26} {detail}");

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Invoke(action);
    }

    private static string ParseOut(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == "--out")
                return args[i + 1];
        return Path.Combine(Directory.GetCurrentDirectory(), "preview-out");
    }
}
