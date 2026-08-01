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
using Diz.Core.commands;
using Diz.Core.Interfaces;
using Diz.Cpu._65816;
using Diz.Ui.Avalonia;                    // DizAvaloniaApp, AvaloniaLabelEditorView, AvaloniaFileDialogService, LabelEditorWindow + MarkManyWindow + GotoWindow + HarshAutoStepWindow (internal)
using Diz.Ui.ViewModels.Goto;             // GotoViewModel
using Diz.Ui.ViewModels.HarshAutoStep;    // HarshAutoStepViewModel
using Diz.Ui.ViewModels.Labels;           // LabelEditorViewModel, ILabelEditorViewModel, ILabelRowViewModel, LabelField
using Diz.Ui.ViewModels.MarkMany;         // MarkManyViewModel, AddressRangeViewModel
using Diz.Ui.ViewModels.MisalignmentChecker; // MisalignmentCheckerViewModel
using Diz.Ui.ViewModels.Regions;          // RegionListViewModel, IRegionListViewModel, IRegionRowViewModel, RegionField

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

        // ------------------------------------------------------------------ MARK MANY WINDOW
        MarkManyScenes(outDir, report);

        // ------------------------------------------------------------------ GOTO WINDOW
        ProbeTextChangedTiming(report);
        GotoScenes(outDir, report);

        // ------------------------------------------------------------------ HARSH AUTO STEP WINDOW
        HarshAutoStepScenes(outDir, report);

        // ------------------------------------------------------------------ CHECKER WINDOWS
        MisalignmentCheckerScenes(outDir, report);
        InOutPointCheckerScenes(outDir, report);

        // ------------------------------------------------------------------ REGION LIST WINDOW
        RegionListScenes(outDir, report);

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

    // ------------------------------------------------------------------ mark many window

    /// <summary>
    /// Renders the Avalonia mark-many window in three states and drives it with simulated
    /// input. Unlike the label editor this window needs a ROM, so the fixture builds a tiny
    /// in-memory one. Each scene gets a FRESH window + ViewModel, because the real window is
    /// created per invocation and completes a task when it closes.
    /// </summary>
    private static void MarkManyScenes(string outDir, List<string> report)
    {
        Console.WriteLine();
        Console.WriteLine("[mark many window]");

        // ---- scene 1: default (Flag selected, flag combo showing, valid range)
        var (defaultWindow, defaultVm) = OpenMarkMany();
        Capture(defaultWindow, Path.Combine(outDir, "markmany-default.png"),
            $"mark many: default, property=Flag, range {defaultVm.Range.StartText}..{defaultVm.Range.EndText} " +
            $"({defaultVm.Range.CountText} bytes)");

        // ---- scene 2: validation error (data bank out of range -> OK disabled + message)
        var (invalidWindow, invalidVm) = OpenMarkMany();
        invalidVm.SelectedProperty = MarkCommand.MarkManyProperty.DataBank;
        invalidVm.DataBankValue = 0x1FF;
        Pump();
        Capture(invalidWindow, Path.Combine(outDir, "markmany-validation-error.png"),
            $"mark many: data bank $1FF rejected -- '{TagText(invalidWindow, "error-text")}'");

        // ---- scene 3: a different property selected (M Flag -> the 16/8-bit combo)
        var (mflagWindow, mflagVm) = OpenMarkMany();
        mflagVm.SelectedProperty = MarkCommand.MarkManyProperty.MFlag;
        mflagVm.RegisterWidthIs8Bit = true;
        Pump();
        Capture(mflagWindow, Path.Combine(outDir, "markmany-property-switched.png"),
            "mark many: property=M Flag, value combo = 8-Bit");

        // ---- interaction probes, on their own window
        ProbeMarkMany(outDir, report);
        ProbeMarkManyTypingIsNotRewritten(outDir, report);

        defaultWindow.Close();
        invalidWindow.Close();
        mflagWindow.Close();
        Pump();
    }

    private static (MarkManyWindow window, MarkManyViewModel<ISnesData> vm) OpenMarkMany(
        int start = 0x100, int count = 0x10)
    {
        var vm = new MarkManyViewModel<ISnesData>(PreviewFixture.BuildSnesData(), start, count);
        var window = new MarkManyWindow();
        window.AttachViewModel(vm);
        window.Show();
        Pump();
        return (window, vm);
    }

    private static void ProbeMarkMany(string outDir, List<string> report)
    {
        MarkManyWindow? window = null;
        try
        {
            var (w, vm) = OpenMarkMany();
            window = w;

            // ---- 1: pick "Data Bank" in the property selector
            var propertyCombo = FindByTag<ComboBox>(window, "property-combo");
            if (propertyCombo == null)
            {
                Record(report, "markmany property combo", "HARNESS-FAIL", "property ComboBox not found");
                return;
            }

            var beforeProperty = vm.SelectedProperty;
            propertyCombo.SelectedIndex = 1; // "Data Bank"
            Pump();
            var propertyOk = vm.SelectedProperty == MarkCommand.MarkManyProperty.DataBank;
            Console.WriteLine($"  property combo -> index 1; vm.SelectedProperty {beforeProperty} -> {vm.SelectedProperty}");
            Record(report, "markmany property select", propertyOk ? "PASS" : "APP-FAIL",
                $"vm.SelectedProperty {beforeProperty} -> {vm.SelectedProperty} (wanted DataBank)");

            // the value editor must have swapped to the register box
            var regBox = FindByTag<TextBox>(window, "reg-value");
            var flagCombo = FindByTag<ComboBox>(window, "flag-combo");
            var widgetOk = regBox is { IsVisible: true } && flagCombo is { IsVisible: false };
            Record(report, "markmany value widget swap", widgetOk ? "PASS" : "APP-FAIL",
                $"reg box visible={regBox?.IsVisible}, flag combo visible={flagCombo?.IsVisible}");

            // ---- 2: type a valid data bank into the value box
            if (regBox == null)
            {
                Record(report, "markmany value typing", "HARNESS-FAIL", "register value TextBox not found");
                return;
            }

            TypeIntoBox(window, regBox, "7E");
            var valueOk = vm.DataBankValue == 0x7E && (regBox.Text ?? "") == "7E";
            Console.WriteLine($"  typed '7E' into value box; box='{regBox.Text}'; vm.DataBankValue=${vm.DataBankValue:X}");
            Record(report, "markmany value typing", valueOk ? "PASS" : "APP-FAIL",
                $"box='{regBox.Text}', vm.DataBankValue=${vm.DataBankValue:X} (wanted $7E)");

            // ---- 3: type a byte count into the range
            var countBox = FindByTag<TextBox>(window, "count-text");
            var startBox = FindByTag<TextBox>(window, "start-text");
            var endBox = FindByTag<TextBox>(window, "end-text");
            if (countBox == null || startBox == null || endBox == null)
            {
                Record(report, "markmany range typing", "HARNESS-FAIL",
                    $"range boxes missing (start={startBox != null}, end={endBox != null}, count={countBox != null})");
                return;
            }

            var startBefore = vm.Range.StartIndex;
            var endTextBefore = endBox.Text;
            TypeIntoBox(window, countBox, "20");
            var rangeOk = vm.Range.Count == 0x20 && vm.Range.StartIndex == startBefore;
            // the OTHER fields must refresh, and the field being typed in must keep the typed text
            var refreshOk = (countBox.Text ?? "") == "20" && endBox.Text != endTextBefore &&
                            endBox.Text == vm.Range.EndText;
            Console.WriteLine($"  typed '20' into # bytes; vm.Range.Count={vm.Range.Count:X}; " +
                              $"start {startBefore:X}->{vm.Range.StartIndex:X}; end box '{endTextBefore}'->'{endBox.Text}'");
            Record(report, "markmany range typing", rangeOk ? "PASS" : "APP-FAIL",
                $"vm.Range.Count=0x{vm.Range.Count:X} (wanted 0x20), start held at 0x{vm.Range.StartIndex:X}");
            Record(report, "markmany range refresh", refreshOk ? "PASS" : "APP-FAIL",
                $"typed field kept '{countBox.Text}', end field refreshed to '{endBox.Text}'");

            // ---- 4: an out-of-range value disables OK and shows the ViewModel's reason
            var okButton = FindByTag<Button>(window, "ok-button");
            if (okButton == null)
            {
                Record(report, "markmany OK gating", "HARNESS-FAIL", "OK Button not found");
                return;
            }

            TypeIntoBox(window, regBox, "1FF"); // > $FF for a data bank
            var errorText = TagText(window, "error-text");
            var gatedOk = !okButton.IsEnabled && !string.IsNullOrEmpty(errorText) && !vm.CanBuildMarkCommand;
            Console.WriteLine($"  typed '1FF'; OK enabled={okButton.IsEnabled}; error='{errorText}'");
            Record(report, "markmany OK gated on invalid", gatedOk ? "PASS" : "APP-FAIL",
                $"OK enabled={okButton.IsEnabled}, message='{errorText}'");

            Pump();
            Capture(window, Path.Combine(outDir, "markmany-probe-invalid.png"),
                "mark many: after typing an out-of-range data bank (OK greyed, reason shown)");

            // ---- 5: correct it, then confirm -- the built command must match what is on screen
            TypeIntoBox(window, regBox, "C0");
            var reenabledOk = okButton.IsEnabled && string.IsNullOrEmpty(TagText(window, "error-text"));
            Record(report, "markmany OK re-enabled", reenabledOk ? "PASS" : "APP-FAIL",
                $"OK enabled={okButton.IsEnabled}, message='{TagText(window, "error-text")}'");

            var okPoint = CenterInWindow(okButton, window);
            if (okPoint == null)
            {
                Record(report, "markmany confirm", "HARNESS-FAIL", "OK button has no on-screen position");
                return;
            }

            Click(window, okPoint.Value);
            Pump();

            var confirmed = window.Completion.IsCompleted && window.Completion.Result;
            var command = vm.BuildMarkCommand();
            var commandOk = confirmed && command != null &&
                            command.Property == MarkCommand.MarkManyProperty.DataBank &&
                            Equals(command.Value, 0xC0) &&
                            command.Start == vm.Range.StartIndex &&
                            command.Count == vm.Range.Count;
            Console.WriteLine($"  clicked OK; completion={(window.Completion.IsCompleted ? window.Completion.Result.ToString() : "<pending>")}; " +
                              $"command={(command == null ? "<null>" : $"{command.Property} value={command.Value} start=0x{command.Start:X} count=0x{command.Count:X}")}");
            Record(report, "markmany confirm -> command", commandOk ? "PASS" : "APP-FAIL",
                command == null
                    ? $"confirmed={confirmed}, BuildMarkCommand() returned null"
                    : $"confirmed={confirmed}, {command.Property} value={command.Value} start=0x{command.Start:X} count=0x{command.Count:X}");
        }
        catch (Exception ex)
        {
            Record(report, "markmany", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    /// <summary>
    /// The range field the user is typing in must keep exactly what they typed, keystroke by
    /// keystroke -- the mark-many counterpart of the goto window's probe of the same name.
    ///
    /// Start/end/count are three views of one range, so moving any of them rewrites the other
    /// two, and each of those rewrites is a chance for the range to come back around into the
    /// field under the caret. The address typed is a HiROM MIRROR BANK ($40:0200 is the same ROM
    /// byte as $C0:0200), so the range's own text for it differs from what was typed and a round
    /// trip is visible rather than silent.
    /// </summary>
    private static void ProbeMarkManyTypingIsNotRewritten(string outDir, List<string> report)
    {
        MarkManyWindow? window = null;
        try
        {
            var (w, vm) = OpenMarkMany();
            window = w;

            var startBox = FindByTag<TextBox>(window, "start-text");
            var endBox = FindByTag<TextBox>(window, "end-text");
            var countBox = FindByTag<TextBox>(window, "count-text");
            if (startBox == null || endBox == null || countBox == null)
            {
                Record(report, "markmany typing not rewritten", "HARNESS-FAIL",
                    $"range boxes missing (start={startBox != null}, end={endBox != null}, count={countBox != null})");
                return;
            }

            const string typed = "400200";

            var firstDivergence = TypeCharByChar(window, startBox, typed, _ =>
                $"end box '{endBox.Text}', count box '{countBox.Text}'");
            if (firstDivergence == HarnessCouldNotType)
            {
                Record(report, "markmany typing not rewritten", "HARNESS-FAIL",
                    "start box has no on-screen position");
                return;
            }

            var keptTyping = firstDivergence == null;
            Record(report, "markmany typing not rewritten", keptTyping ? "PASS" : "APP-FAIL",
                keptTyping
                    ? $"start box held every prefix of '{typed}' while it had the caret"
                    : firstDivergence!);

            // the range still has to have moved to where the typed address actually is.
            var movedOk = vm.Range.StartIndex == 0x200;
            Console.WriteLine($"  final: start='{startBox.Text}' end='{endBox.Text}' count='{countBox.Text}'; " +
                              $"vm.Range.StartIndex=0x{vm.Range.StartIndex:X} count=0x{vm.Range.Count:X}");
            Record(report, "markmany mirror bank start", movedOk ? "PASS" : "APP-FAIL",
                $"vm.Range.StartIndex=0x{vm.Range.StartIndex:X} (wanted 0x200)");

            Pump();
            Capture(window, Path.Combine(outDir, "markmany-mirror-bank-typed.png"),
                $"mark many: after typing the mirror-bank address '{typed}' into start one key at a " +
                $"time (box reads '{startBox.Text}')");
        }
        catch (Exception ex)
        {
            Record(report, "markmany typing not rewritten", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    // ------------------------------------------------------------------ TextChanged timing

    /// <summary>
    /// Measures a framework behavior every "don't mistake my own write for the user typing"
    /// re-entrancy guard in this assembly depends on: when a TextBox's Text is set from code,
    /// is TextChanged raised BEFORE the setter returns, or posted to a later dispatcher turn?
    ///
    /// A guard that is a bool raised around the write only works in the first case. In the
    /// second, the flag is already back down when the event lands, and the write comes back in
    /// through the input handler indistinguishable from a keystroke.
    ///
    /// Reported, not asserted: it records what this Avalonia build does rather than demanding a
    /// particular answer, so an upgrade that changes the answer shows up in the readout instead
    /// of being silently absorbed.
    /// </summary>
    private static void ProbeTextChangedTiming(List<string> report)
    {
        Console.WriteLine();
        Console.WriteLine("[avalonia TextChanged timing]");

        var box = new TextBox { Text = "start" };
        var window = new Window { Content = box };
        window.Show();
        Pump();

        var insideSetter = false;
        var firedInsideSetter = 0;
        var firedTotal = 0;
        box.TextChanged += (_, _) =>
        {
            firedTotal++;
            if (insideSetter)
                firedInsideSetter++;
        };

        insideSetter = true;
        box.Text = "written-from-code";
        insideSetter = false;
        var afterSetterReturned = firedTotal;
        Pump();

        var synchronous = firedInsideSetter > 0;
        Console.WriteLine($"  box.Text = <new value>: TextChanged fired {firedInsideSetter}x inside the setter, " +
                          $"{afterSetterReturned}x by the time it returned, {firedTotal}x after pumping the dispatcher");
        Record(report, "avalonia TextChanged timing", "INFO",
            synchronous
                ? $"SYNCHRONOUS -- fired inside the .Text setter ({firedInsideSetter} inside, {firedTotal} total)"
                : $"DEFERRED -- 0 fired inside the .Text setter, {firedTotal} after pumping the dispatcher");

        // the same question for the other two widgets whose state these windows write back:
        // a combo's selected index and a radio button's check.
        var combo = new ComboBox { ItemsSource = new[] { "a", "b", "c" }, SelectedIndex = 0 };
        var radio = new RadioButton { IsChecked = false };
        var panel = new StackPanel();
        panel.Children.Add(combo);
        panel.Children.Add(radio);
        var window2 = new Window { Content = panel };
        window2.Show();
        Pump();

        var insideComboSetter = false;
        var comboInside = 0;
        var comboTotal = 0;
        combo.SelectionChanged += (_, _) =>
        {
            comboTotal++;
            if (insideComboSetter)
                comboInside++;
        };
        insideComboSetter = true;
        combo.SelectedIndex = 2;
        insideComboSetter = false;
        Pump();

        var insideRadioSetter = false;
        var radioInside = 0;
        var radioTotal = 0;
        radio.IsCheckedChanged += (_, _) =>
        {
            radioTotal++;
            if (insideRadioSetter)
                radioInside++;
        };
        insideRadioSetter = true;
        radio.IsChecked = true;
        insideRadioSetter = false;
        Pump();

        Console.WriteLine($"  combo.SelectedIndex = 2: SelectionChanged fired {comboInside}x inside the setter, {comboTotal}x total");
        Console.WriteLine($"  radio.IsChecked = true: IsCheckedChanged fired {radioInside}x inside the setter, {radioTotal}x total");
        Record(report, "avalonia combo/radio timing", "INFO",
            $"ComboBox.SelectionChanged {(comboInside > 0 ? "SYNCHRONOUS" : "DEFERRED")} ({comboInside}/{comboTotal}), " +
            $"RadioButton.IsCheckedChanged {(radioInside > 0 ? "SYNCHRONOUS" : "DEFERRED")} ({radioInside}/{radioTotal})");

        window2.Close();
        window.Close();
        Pump();
    }

    // ------------------------------------------------------------------ goto window

    /// <summary>
    /// Renders the Avalonia goto window and drives it with simulated input. Like mark-many it
    /// needs a ROM (it converts SNES addresses to ROM file offsets and rejects anything outside
    /// the ROM), so the fixture's tiny in-memory HiROM is reused. Each scene gets a FRESH window
    /// + ViewModel, because the real window is created per invocation and completes a task when
    /// it closes.
    /// </summary>
    private static void GotoScenes(string outDir, List<string> report)
    {
        Console.WriteLine();
        Console.WriteLine("[goto window]");

        // ---- scene 1: default (seeded from a ROM file offset, both boxes valid)
        var (defaultWindow, defaultVm) = OpenGoto();
        Capture(defaultWindow, Path.Combine(outDir, "goto-default.png"),
            $"goto: default, SNES '{defaultVm.SnesText}' / PC '{defaultVm.PcText}', Go enabled");

        // ---- scene 2: an address that means nothing -- Go greyed, the ViewModel's reason shown.
        // Driven through the widget rather than by assigning the ViewModel, because the
        // ViewModel deliberately withholds the notification for text it kept exactly as given.
        var (invalidWindow, _) = OpenGoto();
        var invalidBox = FindByTag<TextBox>(invalidWindow, "snes-text");
        if (invalidBox != null)
            TypeIntoBox(invalidWindow, invalidBox, "ZZZZ");
        Pump();
        Capture(invalidWindow, Path.Combine(outDir, "goto-validation-error.png"),
            $"goto: 'ZZZZ' rejected -- '{TagText(invalidWindow, "error-text")}'");

        // ---- interaction probes, on their own window
        ProbeGoto(outDir, report);
        ProbeGotoTypingIsNotRewritten(outDir, report);

        defaultWindow.Close();
        invalidWindow.Close();
        Pump();
    }

    /// <summary>
    /// The box the user is typing in must keep exactly what they typed, keystroke by keystroke.
    ///
    /// The earlier goto probes all type addresses whose round trip is the identity ("C00200" ->
    /// offset 200 -> "C00200"), so they cannot see a box being rewritten from its own mirror.
    /// This one types a MIRROR-BANK address: HiROM ignores the top two bank bits, so $40:0200
    /// and $C0:0200 are the same ROM byte, and converting the offset back produces "C00200" --
    /// a different string from the one on screen. Anything that round-trips the mirror back into
    /// the edited box therefore replaces the user's text mid-word.
    ///
    /// Typed one character at a time, because that is how the failure shows up in use: the
    /// prefixes of a real address ("4", "40", "400", ...) mostly name nowhere, so each keystroke
    /// moves the other box too, and every one of those moves is an opportunity to bounce back.
    /// </summary>
    private static void ProbeGotoTypingIsNotRewritten(string outDir, List<string> report)
    {
        GotoWindow? window = null;
        try
        {
            var (w, vm) = OpenGoto();
            window = w;

            var snesBox = FindByTag<TextBox>(window, "snes-text");
            var pcBox = FindByTag<TextBox>(window, "pc-text");
            if (snesBox == null || pcBox == null)
            {
                Record(report, "goto typing not rewritten", "HARNESS-FAIL",
                    $"snes box={snesBox != null}, pc box={pcBox != null}");
                return;
            }

            const string typed = "400200";

            var firstDivergence = TypeCharByChar(window, snesBox, typed, i =>
                $"pc box '{pcBox.Text}'");
            if (firstDivergence == HarnessCouldNotType)
            {
                Record(report, "goto typing not rewritten", "HARNESS-FAIL", "SNES box has no on-screen position");
                return;
            }

            var keptTyping = firstDivergence == null;
            Record(report, "goto typing not rewritten", keptTyping ? "PASS" : "APP-FAIL",
                keptTyping
                    ? $"SNES box held every prefix of '{typed}' while it had the caret"
                    : firstDivergence!);

            // the mirror still has to work: the offset box must show where $40:0200 actually is.
            var mirrorOk = (pcBox.Text ?? "") == "200" && vm.ResultPcOffset == 0x200;
            Console.WriteLine($"  final: snes='{snesBox.Text}' pc='{pcBox.Text}' ResultPcOffset={Hex(vm.ResultPcOffset)}");
            Record(report, "goto mirror bank offset", mirrorOk ? "PASS" : "APP-FAIL",
                $"pc box '{pcBox.Text}' (wanted '200'), ResultPcOffset={Hex(vm.ResultPcOffset)} (wanted 0x200)");

            Pump();
            Capture(window, Path.Combine(outDir, "goto-mirror-bank-typed.png"),
                $"goto: after typing the mirror-bank address '{typed}' one key at a time " +
                $"(box reads '{snesBox.Text}')");
        }
        catch (Exception ex)
        {
            Record(report, "goto typing not rewritten", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    private static (GotoWindow window, GotoViewModel vm) OpenGoto(int startPcOffset = 0x100)
    {
        var snesData = PreviewFixture.BuildSnesData();
        var vm = new GotoViewModel(snesData, snesData.GetRomSize(), startPcOffset);
        var window = new GotoWindow();
        // true selects the SNES ADDRESS box (see IGotoView).
        window.AttachViewModel(vm, selectSnesAddrInitially: true);
        window.Show();
        Pump();
        return (window, vm);
    }

    private static void ProbeGoto(string outDir, List<string> report)
    {
        GotoWindow? window = null;
        try
        {
            var (w, vm) = OpenGoto();
            window = w;

            var snesBox = FindByTag<TextBox>(window, "snes-text");
            var pcBox = FindByTag<TextBox>(window, "pc-text");
            var goButton = FindByTag<Button>(window, "go-button");
            if (snesBox == null || pcBox == null || goButton == null)
            {
                Record(report, "goto widgets", "HARNESS-FAIL",
                    $"snes box={snesBox != null}, pc box={pcBox != null}, Go button={goButton != null}");
                return;
            }

            // ---- 1: typing a SNES address moves the PC box, and leaves the typed text alone
            TypeIntoBox(window, snesBox, "C00200");
            var snesOk = (snesBox.Text ?? "") == "C00200" && (pcBox.Text ?? "") == "200" &&
                         vm.ResultPcOffset == 0x200;
            Console.WriteLine($"  typed 'C00200' into the SNES box; snes='{snesBox.Text}' pc='{pcBox.Text}' " +
                              $"ResultPcOffset={Hex(vm.ResultPcOffset)}");
            Record(report, "goto snes typing", snesOk ? "PASS" : "APP-FAIL",
                $"snes box kept '{snesBox.Text}', pc box -> '{pcBox.Text}' (wanted '200'), ResultPcOffset={Hex(vm.ResultPcOffset)}");

            // ---- 2: and the mirror direction
            TypeIntoBox(window, pcBox, "300");
            var pcOk = (pcBox.Text ?? "") == "300" && (snesBox.Text ?? "") == "C00300" &&
                       vm.ResultPcOffset == 0x300;
            Console.WriteLine($"  typed '300' into the PC box; pc='{pcBox.Text}' snes='{snesBox.Text}' " +
                              $"ResultPcOffset={Hex(vm.ResultPcOffset)}");
            Record(report, "goto pc typing", pcOk ? "PASS" : "APP-FAIL",
                $"pc box kept '{pcBox.Text}', snes box -> '{snesBox.Text}' (wanted 'C00300'), ResultPcOffset={Hex(vm.ResultPcOffset)}");

            // ---- 3: pasting a label out of the disassembly leaves the bare address behind.
            // This is the ONE case where the box being typed into is rewritten.
            TypeIntoBox(window, snesBox, "CODE_C00123");
            var pasteOk = (snesBox.Text ?? "") == "C00123" && (pcBox.Text ?? "") == "123" &&
                          vm.ResultPcOffset == 0x123;
            Console.WriteLine($"  pasted 'CODE_C00123' into the SNES box; snes='{snesBox.Text}' pc='{pcBox.Text}'");
            Record(report, "goto label paste rewrite", pasteOk ? "PASS" : "APP-FAIL",
                $"snes box rewritten to '{snesBox.Text}' (wanted 'C00123'), pc box '{pcBox.Text}' (wanted '123')");

            Pump();
            Capture(window, Path.Combine(outDir, "goto-label-paste.png"),
                "goto: after pasting the label 'CODE_C00123' (box rewritten to the bare address)");

            // ---- 4: text that names nowhere disables Go and shows the ViewModel's reason
            TypeIntoBox(window, snesBox, "ZZZZ");
            var errorText = TagText(window, "error-text");
            var gatedOk = !goButton.IsEnabled && errorText == GotoViewModel.InvalidSnesAddressMessage &&
                          !vm.CanConfirm && vm.ResultPcOffset == null;
            Console.WriteLine($"  typed 'ZZZZ'; Go enabled={goButton.IsEnabled}; error='{errorText}'; " +
                              $"ResultPcOffset={Hex(vm.ResultPcOffset)}");
            Record(report, "goto invalid gates Go", gatedOk ? "PASS" : "APP-FAIL",
                $"Go enabled={goButton.IsEnabled}, message='{errorText}', ResultPcOffset={Hex(vm.ResultPcOffset)}");

            Pump();
            Capture(window, Path.Combine(outDir, "goto-probe-invalid.png"),
                "goto: after typing an address that means nothing (Go greyed, reason shown)");

            // ---- 5: correct it, then confirm -- the result must match what is on screen
            TypeIntoBox(window, snesBox, "C00456");
            var reenabledOk = goButton.IsEnabled && string.IsNullOrEmpty(TagText(window, "error-text"));
            Record(report, "goto Go re-enabled", reenabledOk ? "PASS" : "APP-FAIL",
                $"Go enabled={goButton.IsEnabled}, message='{TagText(window, "error-text")}'");

            var goPoint = CenterInWindow(goButton, window);
            if (goPoint == null)
            {
                Record(report, "goto confirm", "HARNESS-FAIL", "Go button has no on-screen position");
                return;
            }

            var pcTextOnScreen = pcBox.Text ?? "";
            Click(window, goPoint.Value);
            Pump();

            var confirmed = window.Completion.IsCompleted && window.Completion.Result;
            var boxesAgree = int.TryParse(pcTextOnScreen, System.Globalization.NumberStyles.HexNumber,
                                 System.Globalization.CultureInfo.InvariantCulture, out var fromBox) &&
                             vm.ResultPcOffset == fromBox;
            var confirmOk = confirmed && vm.ResultPcOffset == 0x456 && boxesAgree;
            Console.WriteLine($"  clicked Go; completion={(window.Completion.IsCompleted ? window.Completion.Result.ToString() : "<pending>")}; " +
                              $"ResultPcOffset={Hex(vm.ResultPcOffset)}; pc box was '{pcTextOnScreen}'");
            Record(report, "goto confirm -> result", confirmOk ? "PASS" : "APP-FAIL",
                $"confirmed={confirmed}, ResultPcOffset={Hex(vm.ResultPcOffset)} == pc box '{pcTextOnScreen}': {boxesAgree}");
        }
        catch (Exception ex)
        {
            Record(report, "goto", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    // ------------------------------------------------------------------ harsh auto step window

    /// <summary>
    /// Renders the Avalonia harsh-auto-step window and drives it with simulated input. Like the
    /// other two ROM windows it needs a ROM, so the fixture's tiny in-memory HiROM is reused.
    /// Each scene gets a FRESH window + ViewModel, because the real window is created per
    /// invocation and completes a task when it closes.
    /// </summary>
    private static void HarshAutoStepScenes(string outDir, List<string> report)
    {
        Console.WriteLine();
        Console.WriteLine("[harsh auto step window]");

        // ---- scene 1: default (seeded from a ROM file offset, a full $100 bytes, Go enabled)
        var (defaultWindow, defaultVm) = OpenHarshAutoStep();
        Capture(defaultWindow, Path.Combine(outDir, "harshautostep-default.png"),
            $"harsh auto step: default, range {defaultVm.Range.StartText}..{defaultVm.Range.EndText} " +
            $"({defaultVm.Range.CountText} bytes), Go enabled");

        // ---- scene 2: an empty range -- Go greyed, the ViewModel's reason shown. Driven through
        // the widget rather than by assigning the ViewModel, because the range deliberately
        // withholds the notification for the field being typed into.
        var (emptyWindow, _) = OpenHarshAutoStep();
        var emptyCountBox = FindByTag<TextBox>(emptyWindow, "count-text");
        if (emptyCountBox != null)
            TypeIntoBox(emptyWindow, emptyCountBox, "0");
        Pump();
        Capture(emptyWindow, Path.Combine(outDir, "harshautostep-validation-error.png"),
            $"harsh auto step: zero bytes rejected -- '{TagText(emptyWindow, "error-text")}'");

        // ---- scene 3: the other display mode -- ROM file offsets, decimal
        var (decimalWindow, decimalVm) = OpenHarshAutoStep();
        decimalVm.Range.UseSnesAddresses = false;
        decimalVm.Range.UseHexadecimal = false;
        Pump();
        Capture(decimalWindow, Path.Combine(outDir, "harshautostep-rom-decimal.png"),
            $"harsh auto step: ROM file offset + decimal, range {decimalVm.Range.StartText}.." +
            $"{decimalVm.Range.EndText} ({decimalVm.Range.CountText} bytes)");

        // ---- interaction probes, on their own windows
        ProbeHarshAutoStep(outDir, report);
        ProbeHarshAutoStepTypingIsNotRewritten(outDir, report);

        defaultWindow.Close();
        emptyWindow.Close();
        decimalWindow.Close();
        Pump();
    }

    private static (HarshAutoStepWindow window, HarshAutoStepViewModel vm) OpenHarshAutoStep(
        int startPcOffset = 0x100)
    {
        var snesData = PreviewFixture.BuildSnesData();
        var vm = new HarshAutoStepViewModel(snesData, snesData.GetRomSize(), startPcOffset);
        var window = new HarshAutoStepWindow();
        window.AttachViewModel(vm);
        window.Show();
        Pump();
        return (window, vm);
    }

    private static void ProbeHarshAutoStep(string outDir, List<string> report)
    {
        HarshAutoStepWindow? window = null;
        try
        {
            var (w, vm) = OpenHarshAutoStep();
            window = w;

            var startBox = FindByTag<TextBox>(window, "start-text");
            var endBox = FindByTag<TextBox>(window, "end-text");
            var countBox = FindByTag<TextBox>(window, "count-text");
            var goButton = FindByTag<Button>(window, "go-button");
            if (startBox == null || endBox == null || countBox == null || goButton == null)
            {
                Record(report, "harshautostep widgets", "HARNESS-FAIL",
                    $"start={startBox != null}, end={endBox != null}, count={countBox != null}, " +
                    $"Go={goButton != null}");
                return;
            }

            // ---- 1: typing a START address moves the range; END holds and COUNT follows, and
            // both of the other boxes are refreshed from the ViewModel.
            var endTextBefore = endBox.Text;
            var countTextBefore = countBox.Text;
            TypeIntoBox(window, startBox, "C00180");
            var startOk = vm.Range.StartIndex == 0x180 && vm.Range.EndIndex == 0x1FF &&
                          vm.Range.Count == 0x80 && (startBox.Text ?? "") == "C00180" &&
                          countBox.Text != countTextBefore && countBox.Text == vm.Range.CountText &&
                          endBox.Text == vm.Range.EndText;
            Console.WriteLine($"  typed 'C00180' into start; start=0x{vm.Range.StartIndex:X} " +
                              $"end=0x{vm.Range.EndIndex:X} count=0x{vm.Range.Count:X}; " +
                              $"end box '{endTextBefore}'->'{endBox.Text}', count box '{countTextBefore}'->'{countBox.Text}'");
            Record(report, "harshautostep start typing", startOk ? "PASS" : "APP-FAIL",
                $"start=0x{vm.Range.StartIndex:X} (wanted 0x180), end held 0x{vm.Range.EndIndex:X}, " +
                $"count=0x{vm.Range.Count:X} (wanted 0x80); boxes refreshed to '{endBox.Text}'/'{countBox.Text}'");

            // ---- 2: typing an END address moves COUNT with START held -- and pins the INCLUSIVE
            // arithmetic: the byte named in the End box is part of the range.
            TypeIntoBox(window, endBox, "C002FF");
            var inclusiveOk = vm.Range.StartIndex == 0x180 && vm.Range.EndIndex == 0x2FF &&
                              vm.Range.Count == 0x2FF - 0x180 + 1 &&
                              (endBox.Text ?? "") == "C002FF" && countBox.Text == vm.Range.CountText;
            Console.WriteLine($"  typed 'C002FF' into end; start=0x{vm.Range.StartIndex:X} " +
                              $"end=0x{vm.Range.EndIndex:X} count=0x{vm.Range.Count:X} " +
                              $"(inclusive arithmetic wants 0x{0x2FF - 0x180 + 1:X}); count box '{countBox.Text}'");
            Record(report, "harshautostep end typing (inclusive)", inclusiveOk ? "PASS" : "APP-FAIL",
                $"start held 0x{vm.Range.StartIndex:X}, end=0x{vm.Range.EndIndex:X}, " +
                $"count=0x{vm.Range.Count:X} == end - start + 1 (0x{0x2FF - 0x180 + 1:X})");

            // ---- 3: typing a COUNT moves the END, with START held
            TypeIntoBox(window, countBox, "40");
            var countOk = vm.Range.Count == 0x40 && vm.Range.StartIndex == 0x180 &&
                          vm.Range.EndIndex == 0x1BF && (countBox.Text ?? "") == "40" &&
                          (endBox.Text ?? "") == "C001BF";
            Console.WriteLine($"  typed '40' into # bytes; start=0x{vm.Range.StartIndex:X} " +
                              $"end=0x{vm.Range.EndIndex:X} count=0x{vm.Range.Count:X}; end box '{endBox.Text}'");
            Record(report, "harshautostep count typing", countOk ? "PASS" : "APP-FAIL",
                $"count=0x{vm.Range.Count:X} (wanted 0x40), start held 0x{vm.Range.StartIndex:X}, " +
                $"end box -> '{endBox.Text}' (wanted 'C001BF')");

            // ---- 4: an empty range disables Go and shows the ViewModel's own reason
            TypeIntoBox(window, countBox, "0");
            var errorText = TagText(window, "error-text");
            var gatedOk = !goButton.IsEnabled && errorText == HarshAutoStepViewModel.EmptyRangeMessage &&
                          !vm.CanBuildAutoStepCommand && vm.BuildAutoStepHarshCommand() == null;
            Console.WriteLine($"  typed '0' into # bytes; Go enabled={goButton.IsEnabled}; error='{errorText}'");
            Record(report, "harshautostep empty gates Go", gatedOk ? "PASS" : "APP-FAIL",
                $"Go enabled={goButton.IsEnabled}, message='{errorText}', command={(vm.BuildAutoStepHarshCommand() == null ? "<null>" : "<built>")}");

            Pump();
            Capture(window, Path.Combine(outDir, "harshautostep-probe-invalid.png"),
                "harsh auto step: after asking for zero bytes (Go greyed, reason shown)");

            // ---- 5: correct it, then confirm -- the built command must match what is on screen
            TypeIntoBox(window, countBox, "20");
            var reenabledOk = goButton.IsEnabled && string.IsNullOrEmpty(TagText(window, "error-text"));
            Record(report, "harshautostep Go re-enabled", reenabledOk ? "PASS" : "APP-FAIL",
                $"Go enabled={goButton.IsEnabled}, message='{TagText(window, "error-text")}'");

            var goPoint = CenterInWindow(goButton, window);
            if (goPoint == null)
            {
                Record(report, "harshautostep confirm", "HARNESS-FAIL", "Go button has no on-screen position");
                return;
            }

            var startTextOnScreen = startBox.Text ?? "";
            var countTextOnScreen = countBox.Text ?? "";
            Click(window, goPoint.Value);
            Pump();

            var confirmed = window.Completion.IsCompleted && window.Completion.Result;
            var command = vm.BuildAutoStepHarshCommand();
            var screenCountParsed = int.TryParse(countTextOnScreen,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var countFromBox);
            var commandOk = confirmed && command != null && command.Start == 0x180 &&
                            command.Count == 0x20 && screenCountParsed && command.Count == countFromBox &&
                            startTextOnScreen == "C00180";
            Console.WriteLine($"  clicked Go; completion={(window.Completion.IsCompleted ? window.Completion.Result.ToString() : "<pending>")}; " +
                              $"command={(command == null ? "<null>" : $"start=0x{command.Start:X} count=0x{command.Count:X}")}; " +
                              $"boxes were start='{startTextOnScreen}' count='{countTextOnScreen}'");
            Record(report, "harshautostep confirm -> command", commandOk ? "PASS" : "APP-FAIL",
                command == null
                    ? $"confirmed={confirmed}, BuildAutoStepHarshCommand() returned null"
                    : $"confirmed={confirmed}, start=0x{command.Start:X} count=0x{command.Count:X} " +
                      $"== boxes start='{startTextOnScreen}' count='{countTextOnScreen}'");
        }
        catch (Exception ex)
        {
            Record(report, "harshautostep", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    /// <summary>
    /// The range field the user is typing in must keep exactly what they typed, keystroke by
    /// keystroke -- the harsh-auto-step counterpart of the goto and mark-many probes of the same
    /// name, and the exact reproduction that caught the echo bug.
    ///
    /// Start/end/count are three views of one range, so moving any of them rewrites the other
    /// two, and each of those rewrites is a chance for the range to come back around into the
    /// field under the caret. The address typed is a HiROM MIRROR BANK ($40:0200 is the same ROM
    /// byte as $C0:0200), so the range's own text for it differs from what was typed and a round
    /// trip is visible rather than silent.
    ///
    /// Afterwards a different field is edited, which is what makes the canonical conversion
    /// appear: the start box is only ever rewritten once the caret has moved off it.
    /// </summary>
    private static void ProbeHarshAutoStepTypingIsNotRewritten(string outDir, List<string> report)
    {
        HarshAutoStepWindow? window = null;
        try
        {
            var (w, vm) = OpenHarshAutoStep();
            window = w;

            var startBox = FindByTag<TextBox>(window, "start-text");
            var endBox = FindByTag<TextBox>(window, "end-text");
            var countBox = FindByTag<TextBox>(window, "count-text");
            if (startBox == null || endBox == null || countBox == null)
            {
                Record(report, "harshautostep typing not rewritten", "HARNESS-FAIL",
                    $"range boxes missing (start={startBox != null}, end={endBox != null}, count={countBox != null})");
                return;
            }

            const string typed = "400200";

            var firstDivergence = TypeCharByChar(window, startBox, typed, _ =>
                $"end box '{endBox.Text}', count box '{countBox.Text}'");
            if (firstDivergence == HarnessCouldNotType)
            {
                Record(report, "harshautostep typing not rewritten", "HARNESS-FAIL",
                    "start box has no on-screen position");
                return;
            }

            var keptTyping = firstDivergence == null;
            Record(report, "harshautostep typing not rewritten", keptTyping ? "PASS" : "APP-FAIL",
                keptTyping
                    ? $"start box held every prefix of '{typed}' while it had the caret"
                    : firstDivergence!);

            // the range still has to have moved to where the typed address actually is...
            var movedOk = vm.Range.StartIndex == 0x200;

            Pump();
            Capture(window, Path.Combine(outDir, "harshautostep-mirror-bank-typed.png"),
                $"harsh auto step: after typing the mirror-bank address '{typed}' into start one key " +
                $"at a time (box reads '{startBox.Text}')");

            // ...and once the caret moves to another field, the start box shows the CANONICAL
            // bank for that same ROM byte rather than the mirror the user typed.
            TypeIntoBox(window, countBox, "8");
            var canonicalOk = (startBox.Text ?? "") == "C00200" && vm.Range.StartIndex == 0x200;
            Console.WriteLine($"  after typing '8' into # bytes: start box '{startBox.Text}' " +
                              $"end box '{endBox.Text}'; vm.Range.StartIndex=0x{vm.Range.StartIndex:X} " +
                              $"count=0x{vm.Range.Count:X}");
            Record(report, "harshautostep mirror bank start", movedOk && canonicalOk ? "PASS" : "APP-FAIL",
                $"vm.Range.StartIndex=0x{vm.Range.StartIndex:X} (wanted 0x200), start box now " +
                $"'{startBox.Text}' (wanted the canonical 'C00200')");
        }
        catch (Exception ex)
        {
            Record(report, "harshautostep typing not rewritten", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    // ------------------------------------------------------------------ misaligned-flags window

    /// <summary>
    /// Renders the Avalonia misaligned-flags window and drives it with simulated input.
    ///
    /// Needs NO ROM: the ViewModel runs a caller-seeded scan delegate, so the harness supplies a
    /// canned result instead of sweeping anything. Each scene gets a FRESH window + ViewModel,
    /// because the real window is created per invocation and completes a task when it closes.
    ///
    /// The state worth looking at hardest is the CAPPED scan: its status sentence runs to about
    /// eighty characters, far wider than the window, so it is the one thing in this window that
    /// can silently lose half of what it says if the status element does not wrap.
    /// </summary>
    private static void MisalignmentCheckerScenes(string outDir, List<string> report)
    {
        Console.WriteLine();
        Console.WriteLine("[misaligned flags window]");

        // ---- scene 1: nothing scanned yet -- empty report, empty status, Fix already available
        var (freshWindow, _) = OpenMisalignmentChecker(() => (0, ""));
        var freshFix = FindByTag<Button>(freshWindow, "fix-button");
        var freshReport = FindByTag<TextBox>(freshWindow, "report-text");
        Capture(freshWindow, Path.Combine(outDir, "misalignmentchecker-default.png"),
            "misaligned flags: opened, nothing scanned yet (report + status empty, Fix enabled)");

        // Fix is deliberately NOT gated on having scanned -- the instruction paragraph offers it
        // as an alternative to reading the report, and the legacy window always allowed it.
        var ungatedOk = freshFix is { IsEnabled: true } &&
                        string.IsNullOrEmpty(TagText(freshWindow, "status-text")) &&
                        string.IsNullOrEmpty(freshReport?.Text);
        Console.WriteLine($"  fresh window: Fix enabled={freshFix?.IsEnabled}, " +
                          $"status='{TagText(freshWindow, "status-text")}', report='{freshReport?.Text}'");
        Record(report, "misalignment fix ungated", ungatedOk ? "PASS" : "APP-FAIL",
            $"Fix enabled={freshFix?.IsEnabled} before any scan, status='{TagText(freshWindow, "status-text")}'");

        // the report is output, never input: there is no writable text anywhere in this window,
        // which is why the code-behind carries no typing/echo guard.
        var readOnlyOk = freshReport is { IsReadOnly: true };
        Record(report, "misalignment report read-only", readOnlyOk ? "PASS" : "APP-FAIL",
            $"report box IsReadOnly={freshReport?.IsReadOnly}");

        // ---- scene 2: a scan that found a handful
        const string foundReport =
            "Misaligned instruction at C00123\r\nMisaligned data at C00456\r\nMisaligned pointer at C0078A";
        var (scannedWindow, scannedVm) = OpenMisalignmentChecker(() => (3, foundReport));
        var scanButton = FindByTag<Button>(scannedWindow, "scan-button");
        var scannedReport = FindByTag<TextBox>(scannedWindow, "report-text");
        var scanPoint = scanButton == null ? null : CenterInWindow(scanButton, scannedWindow);
        if (scanPoint == null)
        {
            Record(report, "misalignment scan click", "HARNESS-FAIL", "Scan button has no on-screen position");
        }
        else
        {
            Click(scannedWindow, scanPoint.Value);
            Pump();
            var scanOk = scannedVm.FoundCount == 3 &&
                         (scannedReport?.Text ?? "") == foundReport &&
                         TagText(scannedWindow, "status-text") == "Found 3 misalignments";
            Console.WriteLine($"  clicked Scan; vm.FoundCount={scannedVm.FoundCount}; " +
                              $"status='{TagText(scannedWindow, "status-text")}'; " +
                              $"report box has {(scannedReport?.Text ?? "").Length} chars");
            Record(report, "misalignment scan click", scanOk ? "PASS" : "APP-FAIL",
                $"vm.FoundCount={scannedVm.FoundCount}, status='{TagText(scannedWindow, "status-text")}', " +
                $"report box == generator text: {(scannedReport?.Text ?? "") == foundReport}");
        }

        Capture(scannedWindow, Path.Combine(outDir, "misalignmentchecker-scanned.png"),
            $"misaligned flags: after Scan -- '{TagText(scannedWindow, "status-text")}', 3-line report");

        var shortStatusHeight = FindByTag<TextBlock>(scannedWindow, "status-text")?.Bounds.Height ?? 0;

        // ---- scene 3: a CAPPED scan. The generator tests its 500-limit once per step and a step
        // can add several findings, so a capped scan reports AT LEAST 500 -- hence the odd count.
        const int cappedCount = MisalignmentCheckerViewModel.FindingLimit + 3;
        var (cappedWindow, cappedVm) = OpenMisalignmentChecker(() => (cappedCount, "...500+ findings..."));
        cappedVm.Scan();
        Pump();
        var cappedStatusBlock = FindByTag<TextBlock>(cappedWindow, "status-text");
        var cappedText = TagText(cappedWindow, "status-text");
        Capture(cappedWindow, Path.Combine(outDir, "misalignmentchecker-capped-status.png"),
            $"misaligned flags: capped scan -- the ~{cappedText.Length}-char status sentence, wrapped");

        // The layout question, asked of the rendered control rather than of the string: the
        // capped sentence must occupy MORE THAN ONE LINE (it wrapped) and must still fit inside
        // the window (it was not simply drawn off the edge). A non-wrapping status would clip
        // exactly the clause saying the ROM was not swept to the end.
        var cappedHeight = cappedStatusBlock?.Bounds.Height ?? 0;
        var cappedWidth = cappedStatusBlock?.Bounds.Width ?? 0;
        var wrapped = cappedHeight > shortStatusHeight && shortStatusHeight > 0;
        var fits = cappedWidth > 0 && cappedWidth <= cappedWindow.Width;
        Console.WriteLine($"  capped status ({cappedText.Length} chars): '{cappedText}'");
        Console.WriteLine($"  status block: short scan {shortStatusHeight:F1}px tall, capped scan " +
                          $"{cappedHeight:F1}px tall x {cappedWidth:F1}px wide (window {cappedWindow.Width}px)");
        Record(report, "misalignment status wraps", wrapped && fits ? "PASS" : "APP-FAIL",
            $"{cappedText.Length}-char sentence: {cappedHeight:F1}px tall vs {shortStatusHeight:F1}px for a " +
            $"short one (wrapped={wrapped}), {cappedWidth:F1}px wide inside a {cappedWindow.Width}px window (fits={fits})");

        // ---- confirm / cancel / closed-without-choosing
        ProbeMisalignmentCheckerAnswers(report);

        freshWindow.Close();
        scannedWindow.Close();
        cappedWindow.Close();
        Pump();
    }

    private static (MisalignmentCheckerWindow window, MisalignmentCheckerViewModel vm)
        OpenMisalignmentChecker(Func<(int found, string reportText)> scan)
    {
        var vm = new MisalignmentCheckerViewModel(scan);
        var window = new MisalignmentCheckerWindow();
        window.AttachViewModel(vm);
        window.Show();
        Pump();
        return (window, vm);
    }

    /// <summary>
    /// The three ways out of the misaligned-flags window, each on its own window because each
    /// completes that window's task for good: Fix confirms, Cancel declines, and closing it any
    /// other way (the X, Escape) counts as declining too.
    /// </summary>
    private static void ProbeMisalignmentCheckerAnswers(List<string> report)
    {
        try
        {
            ProbeButtonAnswer(report, "misalignment fix -> confirm", "fix-button", expected: true,
                open: () => OpenMisalignmentChecker(() => (0, "")).window,
                completion: w => ((MisalignmentCheckerWindow)w).Completion);

            ProbeButtonAnswer(report, "misalignment cancel", "cancel-button", expected: false,
                open: () => OpenMisalignmentChecker(() => (0, "")).window,
                completion: w => ((MisalignmentCheckerWindow)w).Completion);

            // closed without answering: the task must still complete, as a decline.
            var (closedWindow, _) = OpenMisalignmentChecker(() => (0, ""));
            closedWindow.Close();
            Pump();
            var closedOk = closedWindow.Completion.IsCompleted && !closedWindow.Completion.Result;
            Console.WriteLine($"  closed without answering; completion=" +
                              $"{(closedWindow.Completion.IsCompleted ? closedWindow.Completion.Result.ToString() : "<pending>")}");
            Record(report, "misalignment close = cancel", closedOk ? "PASS" : "APP-FAIL",
                $"completion={(closedWindow.Completion.IsCompleted ? closedWindow.Completion.Result.ToString() : "<pending>")} (wanted False)");
        }
        catch (Exception ex)
        {
            Record(report, "misalignment answers", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ in/out-point rescan window

    /// <summary>
    /// Renders the Avalonia in/out-point rescan confirmation and drives it with simulated input.
    /// There is no ViewModel and no ROM: the window explains what a rescan does and takes a yes
    /// or a no, and the rescan itself belongs to whoever opened it.
    /// </summary>
    private static void InOutPointCheckerScenes(string outDir, List<string> report)
    {
        Console.WriteLine();
        Console.WriteLine("[in/out point rescan window]");

        var window = OpenInOutPointChecker();
        var instruction = TagText(window, "instruction-text");
        var rescanButton = FindByTag<Button>(window, "rescan-button");
        Console.WriteLine($"  instruction paragraph: {instruction.Length} chars, " +
                          $"{instruction.Split('\n').Length} lines; Rescan is default={rescanButton?.IsDefault}");
        Capture(window, Path.Combine(outDir, "inoutpointchecker-default.png"),
            $"in/out point rescan: the {instruction.Split('\n').Length}-line explanation, Cancel | Rescan");

        // the whole window is that paragraph, so an empty or clipped one is the only way it can
        // be wrong before a button is pressed.
        var paragraphBlock = FindByTag<TextBlock>(window, "instruction-text");
        var paragraphOk = instruction.Length > 0 && paragraphBlock != null &&
                          paragraphBlock.Bounds.Height > 0 &&
                          paragraphBlock.Bounds.Width <= window.Width;
        Record(report, "inout instruction rendered", paragraphOk ? "PASS" : "APP-FAIL",
            $"{instruction.Length} chars laid out {paragraphBlock?.Bounds.Width:F1}x{paragraphBlock?.Bounds.Height:F1}px " +
            $"inside a {window.Width}x{window.Height}px window");

        window.Close();
        Pump();

        try
        {
            ProbeButtonAnswer(report, "inout rescan -> confirm", "rescan-button", expected: true,
                open: OpenInOutPointChecker,
                completion: w => ((InOutPointCheckerWindow)w).Completion);

            ProbeButtonAnswer(report, "inout cancel", "cancel-button", expected: false,
                open: OpenInOutPointChecker,
                completion: w => ((InOutPointCheckerWindow)w).Completion);

            var closedWindow = OpenInOutPointChecker();
            closedWindow.Close();
            Pump();
            var closedOk = closedWindow.Completion.IsCompleted && !closedWindow.Completion.Result;
            Console.WriteLine($"  closed without answering; completion=" +
                              $"{(closedWindow.Completion.IsCompleted ? closedWindow.Completion.Result.ToString() : "<pending>")}");
            Record(report, "inout close = cancel", closedOk ? "PASS" : "APP-FAIL",
                $"completion={(closedWindow.Completion.IsCompleted ? closedWindow.Completion.Result.ToString() : "<pending>")} (wanted False)");
        }
        catch (Exception ex)
        {
            Record(report, "inout answers", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static InOutPointCheckerWindow OpenInOutPointChecker()
    {
        var window = new InOutPointCheckerWindow();
        window.Show();
        Pump();
        return window;
    }

    /// <summary>
    /// Click one button on a freshly opened window and check the answer its task carries. Shared
    /// by both checker windows, whose entire user interaction is "press one of these".
    /// </summary>
    private static void ProbeButtonAnswer(
        List<string> report, string what, string buttonTag, bool expected,
        Func<Window> open, Func<Window, Task<bool>> completion)
    {
        Window? window = null;
        try
        {
            window = open();
            var button = FindByTag<Button>(window, buttonTag);
            var point = button == null ? null : CenterInWindow(button, window);
            if (point == null)
            {
                Record(report, what, "HARNESS-FAIL", $"'{buttonTag}' has no on-screen position");
                return;
            }

            Click(window, point.Value);
            Pump();

            var task = completion(window);
            var ok = task.IsCompleted && task.Result == expected;
            Console.WriteLine($"  clicked '{buttonTag}'; completion=" +
                              $"{(task.IsCompleted ? task.Result.ToString() : "<pending>")} (wanted {expected})");
            Record(report, what, ok ? "PASS" : "APP-FAIL",
                $"completion={(task.IsCompleted ? task.Result.ToString() : "<pending>")} (wanted {expected})");
        }
        catch (Exception ex)
        {
            Record(report, what, "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    private static string Hex(int? value) => value == null ? "<null>" : $"0x{value.Value:X}";

    /// <summary>Sentinel <see cref="TypeCharByChar"/> returns when the gesture couldn't be performed.</summary>
    private const string HarnessCouldNotType = "<harness: box has no on-screen position>";

    /// <summary>
    /// Replace a box's contents by typing <paramref name="text"/> ONE KEY AT A TIME, checking
    /// after every keystroke that the box holds exactly the prefix typed so far. Returns null
    /// when it always did, otherwise a description of the FIRST keystroke after which it did not.
    ///
    /// One key at a time rather than one KeyTextInput of the whole string, because a field that
    /// mirrors another only rewrites itself when the mirror moves, and it is the half-typed
    /// prefixes that move it.
    /// </summary>
    private static string? TypeCharByChar(Window window, TextBox box, string text, Func<int, string> describeOthers)
    {
        var pt = CenterInWindow(box, window);
        if (pt == null)
            return HarnessCouldNotType;

        Click(window, pt.Value);
        window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
        window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
        Pump();

        string? firstDivergence = null;
        for (var i = 0; i < text.Length; i++)
        {
            window.KeyTextInput(text[i].ToString());
            Pump();

            var expected = text[..(i + 1)];
            var actual = box.Text ?? "";
            Console.WriteLine($"  keystroke {i + 1} '{text[i]}': box expected '{expected}', is '{actual}'; {describeOthers(i)}");
            if (firstDivergence == null && actual != expected)
                firstDivergence = $"after keystroke {i + 1} ('{text[i]}') the box read '{actual}', not '{expected}'";
        }

        return firstDivergence;
    }

    /// <summary>Click into a TextBox, select all, and type -- i.e. replace its contents.</summary>
    private static void TypeIntoBox(Window window, TextBox box, string text)
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

    private static T? FindByTag<T>(Window window, string tag) where T : Control =>
        window.GetVisualDescendants().OfType<T>().FirstOrDefault(c => Equals(c.Tag, tag));

    private static string TagText(Window window, string tag) =>
        FindByTag<TextBlock>(window, tag)?.Text ?? "";

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

    // ------------------------------------------------------------------ region list window

    /// <summary>
    /// Renders the Avalonia region editor in four states and drives it with simulated input.
    /// Unlike the label editor this window needs a region provider, so the fixture's tiny
    /// in-memory ROM is seeded with a spread of realistic regions: plain assembly banks, a
    /// separate-file bank, and typed assets (gfx + BRR).
    ///
    /// The first PNG is also the DataGrid THEME check: the control ships in its own package and
    /// its theme in a separate style resource, so a missing StyleInclude shows up here as an
    /// untemplated, effectively invisible grid rather than as a build error.
    /// </summary>
    private static void RegionListScenes(string outDir, List<string> report)
    {
        Console.WriteLine();
        Console.WriteLine("[region list window]");

        // ---- scene 1: populated master grid, default sort (Start ascending)
        var (window, vm, _) = OpenRegionList();
        Console.WriteLine($"  {vm.RegionCount} regions, {vm.Rows.Count} rows, " +
                          $"sort={vm.SortField} descending={vm.SortDescending}");
        Capture(window, Path.Combine(outDir, "regionlist-default.png"),
            $"region list: {vm.RegionCount} regions, master grid + read-only details pane, " +
            "sorted by Start ascending");

        ProbeRegionGridPopulates(window, vm, report);
        ProbeRegionSelection(window, vm, report);

        // ---- scene 2: sorted descending, driven through the header (the VM owns the order)
        ProbeRegionHeaderSort(window, vm, report);
        Capture(window, Path.Combine(outDir, "regionlist-sorted-desc.png"),
            $"region list: header click sorted by {vm.SortField} " +
            $"{(vm.SortDescending ? "descending" : "ascending")} - arrow in the header, " +
            "highest Start first");

        ProbeRegionSelectionSurvivesResort(window, vm, report);
        window.Close();
        Pump();

        // ---- scene 3: a row the rules refuse, flagged without committing
        var (badWindow, badVm, _) = OpenRegionList();
        var victim = badVm.Rows.First(r => r.RegionNameText == "title_gfx");
        badVm.SelectedRow = victim;
        var refusal = badVm.CommitField(victim, RegionField.RegionName, "   ");

        // a second flagged row, deliberately NOT the selected one: the tint is what an unselected
        // bad row shows, and the selection brush paints over it on the row the user is in - which
        // is why the marker also lives in the row header.
        var alsoBad = badVm.Rows.First(r => r.RegionNameText == "map_tiles");
        badVm.CommitField(alsoBad, RegionField.End, "C2FFFF"); // end before start: refused
        Pump();
        Console.WriteLine($"  refused edit: valid={refusal.IsValid}, row.HasError={victim.HasError}, " +
                          $"model name still '{victim.LastGoodTextFor(RegionField.RegionName)}', " +
                          $"status='{badVm.StatusText}'");
        Capture(badWindow, Path.Combine(outDir, "regionlist-bad-row.png"),
            $"region list: a blanked Region Name was refused - row tinted + tooltip, status says " +
            $"'{badVm.StatusText}', the model still holds " +
            $"'{victim.LastGoodTextFor(RegionField.RegionName)}'");
        badWindow.Close();
        Pump();

        // ---- add / delete, on their own window (both change the region collection)
        ProbeRegionAddAndDelete(outDir, report);

        // ---- scene 4: whole-list problems (two asset regions overlapping)
        ProbeRegionProblemPanel(outDir, report);

        // ---- the details pane: the only place this window edits anything
        ProbeRegionDetailTyping(report);
        ProbeRegionDetailAssetFields(outDir, report);
        ProbeRegionDetailValidation(outDir, report);
        ProbeRegionDetailLostFocusAndEscape(report);
        ProbeRegionDetailClosedValueSnapBack(report);
        ProbeRegionDetailMasterSync(report);
        ProbeRegionDeleteKey(report);
        ProbeRegionCloseHides(report);

        // ---- scene 5: the same window under the DARK theme variant, because the DataGrid's
        // theme carries its own light/dark dictionaries and a missing one shows up as unreadable
        // (not as an error). The app pins Light by default; DIZ_AVALONIA_THEME=dark selects this.
        var (darkWindow, darkVm, _) = OpenRegionList();
        darkWindow.RequestedThemeVariant = global::Avalonia.Styling.ThemeVariant.Dark;
        Pump();
        Capture(darkWindow, Path.Combine(outDir, "regionlist-dark.png"),
            $"region list: {darkVm.RegionCount} regions under the Dark theme variant " +
            "(DIZ_AVALONIA_THEME=dark)");
        darkWindow.Close();
        Pump();
    }

    /// <summary>The columns the master grid is supposed to show, in order.</summary>
    private static readonly string[] ExpectedRegionColumns =
    [
        "Start", "End", "Length", "Region Name", "Label Context", "Priority",
        "Separate File", "Export Type",
    ];

    private static (RegionListWindow window, IRegionListViewModel vm, IRegionProvider provider)
        OpenRegionList(Action<IRegionProvider>? seed = null)
    {
        var provider = PreviewFixture.BuildSnesData();
        (seed ?? SeedRegions)(provider);

        var vm = new RegionListViewModel(provider, notificationMarshaller: RunOnUiThread);
        var window = new RegionListWindow();
        window.AttachViewModel(vm);
        window.Show();
        Pump();
        return (window, vm, provider);
    }

    /// <summary>
    /// A spread that exercises every role the grid distinguishes: plain assembly, a
    /// separate-file bank (one bank, as the rules require), typed gfx and BRR assets, and an
    /// annotation region in WRAM carrying a label context. All of it is valid, so the problem
    /// panel starts empty and the scenes that want a problem can create one deliberately.
    /// </summary>
    private static void SeedRegions(IRegionProvider provider)
    {
        AddRegion(provider, "bank_C0_code", 0xC00000, 0xC0FFFF);
        AddRegion(provider, "bank_C1_code", 0xC10000, 0xC1FFFF, separateFile: true);
        // 0x60 bytes: a whole number of 4bpp cells at cell_h 8 (32 bytes each) AND at cell_h 12
        // (48 bytes each), so the details-pane probe can retype the JSON options and have the
        // result be legal rather than merely refused.
        AddRegion(provider, "title_gfx", 0xC20000, 0xC2005F, RegionExportType.Asset,
            assetType: "gfx.snes.4bpp", assetName: "gfx/title.png", assetOptions: "{\"cell_h\": 8}");
        AddRegion(provider, "intro_song", 0xC20100, 0xC20111, RegionExportType.Asset,
            assetType: "audio.snes.brr", assetName: "audio/intro.brr");
        AddRegion(provider, "map_tiles", 0xC30000, 0xC3007F, RegionExportType.Asset,
            assetType: "gfx.snes.2bpp", assetName: "gfx/map.png", assetOptions: "{\"cell_h\": 8}");
        AddRegion(provider, "battle_scratch", 0x7E0000, 0x7E00FF, context: "battle", priority: 5);
        AddRegion(provider, "sram_notes", 0x700000, 0x7000FF, priority: 1);
    }

    /// <summary>A region straddling a bank boundary: the one shape that makes "export me to my
    /// own file" illegal, and therefore the only way to get a closed-value field refused.</summary>
    private static void SeedCrossBankRegions(IRegionProvider provider)
    {
        AddRegion(provider, "spans_two_banks", 0xC0FF00, 0xC1007F);
        AddRegion(provider, "one_bank", 0xC20000, 0xC200FF);
    }

    /// <summary>Two asset regions deliberately covering the same bytes - the whole-list check
    /// that has no home on any single row.</summary>
    private static void SeedOverlappingRegions(IRegionProvider provider)
    {
        AddRegion(provider, "bank_C0_code", 0xC00000, 0xC0FFFF);
        // Binary rather than Asset: it counts as an asset region for the overlap rule without
        // dragging in a codec's own length arithmetic, so the fix below can move one end freely.
        AddRegion(provider, "sprite_blob_a", 0xC10000, 0xC1007F, RegionExportType.Binary);
        AddRegion(provider, "sprite_blob_b", 0xC10040, 0xC100BF, RegionExportType.Binary);
    }

    private static IRegion AddRegion(
        IRegionProvider provider, string name, int start, int end,
        RegionExportType exportType = RegionExportType.Assembly, bool separateFile = false,
        string assetType = "", string assetName = "", string assetOptions = "",
        string context = "", int priority = 0)
    {
        var region = provider.CreateNewRegion()
                     ?? throw new InvalidOperationException("fixture could not create a region");

        region.RegionName = name;
        region.StartSnesAddress = start;
        region.EndSnesAddress = end;
        region.ExportType = exportType;
        region.ExportSeparateFile = separateFile;
        region.AssetType = assetType;
        region.AssetVersion = "";
        region.AssetName = assetName;
        region.AssetOptions = assetOptions;
        region.ContextToApply = context;
        region.Priority = priority;

        provider.Regions.Add(region);
        return region;
    }

    private static void ProbeRegionGridPopulates(
        RegionListWindow window, IRegionListViewModel vm, List<string> report)
    {
        try
        {
            var grid = FindByTag<DataGrid>(window, "region-grid");
            if (grid == null)
            {
                Record(report, "region grid populates", "HARNESS-FAIL",
                    "no DataGrid tagged 'region-grid' in the visual tree");
                return;
            }

            // headers carry a sort arrow on whichever column the ViewModel is ordering by, so
            // compare on the caption before it.
            var headers = grid.Columns.Select(c => StripSortArrow(c.Header as string ?? "")).ToList();
            var realizedRows = window.GetVisualDescendants().OfType<DataGridRow>().Count();

            var ok = grid.ItemsSource != null &&
                     vm.Rows.Count == vm.RegionCount &&
                     headers.SequenceEqual(ExpectedRegionColumns) &&
                     realizedRows > 0;

            Console.WriteLine($"  columns: {string.Join(" | ", headers)}");
            Console.WriteLine($"  rows: {vm.Rows.Count} in the ViewModel, {realizedRows} realized containers");
            Record(report, "region grid populates", ok ? "PASS" : "APP-FAIL",
                $"{headers.Count} columns [{string.Join(", ", headers)}], {vm.Rows.Count} rows, " +
                $"{realizedRows} realized");
        }
        catch (Exception ex)
        {
            Record(report, "region grid populates", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void ProbeRegionSelection(
        RegionListWindow window, IRegionListViewModel vm, List<string> report)
    {
        try
        {
            var target = vm.Rows[2];
            if (!ClickRegionRow(window, target))
            {
                Record(report, "region selection", "HARNESS-FAIL",
                    $"row '{target.RegionNameText}' has no on-screen position");
                return;
            }

            var ok = ReferenceEquals(vm.SelectedRow, target);
            Console.WriteLine($"  clicked row '{target.RegionNameText}'; " +
                              $"vm.SelectedRow='{vm.SelectedRow?.RegionNameText ?? "<null>"}'");
            Record(report, "region selection", ok ? "PASS" : "APP-FAIL",
                $"clicked '{target.RegionNameText}', vm.SelectedRow='{vm.SelectedRow?.RegionNameText ?? "<null>"}'");
        }
        catch (Exception ex)
        {
            Record(report, "region selection", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void ProbeRegionHeaderSort(
        RegionListWindow window, IRegionListViewModel vm, List<string> report)
    {
        try
        {
            // the grid's own sorting is off, so a header press has exactly one effect: the
            // ViewModel re-orders and the arrow is redrawn from ITS state.
            var startHeader = FindColumnHeader(window, "Start");
            var point = startHeader == null ? null : CenterInWindow(startHeader, window);
            if (point == null)
            {
                Record(report, "region header sorts", "HARNESS-FAIL",
                    "the 'Start' column header has no on-screen position");
                return;
            }

            var before = vm.Rows.Select(r => r.StartText).ToList();
            Click(window, point.Value);
            Pump();

            var after = vm.Rows.Select(r => r.StartText).ToList();
            var arrow = (FindColumnHeader(window, "Start")?.Content as string) ?? "";
            var ok = vm.SortField == RegionField.Start && vm.SortDescending &&
                     after.SequenceEqual(before.AsEnumerable().Reverse()) &&
                     arrow.Contains('▼');

            Console.WriteLine($"  header 'Start' clicked; sort={vm.SortField} desc={vm.SortDescending}; " +
                              $"header now '{arrow}'");
            Console.WriteLine($"  order before: {string.Join(",", before)}");
            Console.WriteLine($"  order after : {string.Join(",", after)}");
            Record(report, "region header sorts", ok ? "PASS" : "APP-FAIL",
                $"vm.SortField={vm.SortField} descending={vm.SortDescending}, header='{arrow}', " +
                $"row order reversed={after.SequenceEqual(before.AsEnumerable().Reverse())}");
        }
        catch (Exception ex)
        {
            Record(report, "region header sorts", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// Re-sorting restreams the whole row collection, which reads to the grid as a reset and
    /// makes it drop its own selection. The ViewModel owns the selection, so the window has to
    /// put it back - otherwise sorting silently clears the details pane and disarms Delete.
    /// </summary>
    private static void ProbeRegionSelectionSurvivesResort(
        RegionListWindow window, IRegionListViewModel vm, List<string> report)
    {
        try
        {
            var grid = FindByTag<DataGrid>(window, "region-grid");
            var target = vm.Rows[1];
            vm.SelectedRow = target;
            Pump();

            vm.SortDescending = !vm.SortDescending;
            Pump();
            Pump(); // the restore is posted past the grid's own selection bookkeeping

            var vmKept = ReferenceEquals(vm.SelectedRow, target);
            var gridKept = ReferenceEquals(grid?.SelectedItem, target);
            Console.WriteLine($"  re-sorted with '{target.RegionNameText}' selected; " +
                              $"vm.SelectedRow='{vm.SelectedRow?.RegionNameText ?? "<null>"}', " +
                              $"grid.SelectedItem='{(grid?.SelectedItem as IRegionRowViewModel)?.RegionNameText ?? "<null>"}'");
            Record(report, "region selection resort", vmKept && gridKept ? "PASS" : "APP-FAIL",
                $"vm kept={vmKept}, grid kept={gridKept} (row '{target.RegionNameText}')");
        }
        catch (Exception ex)
        {
            Record(report, "region selection resort", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// Add, then delete - the two commands that change the region collection. Delete is run
    /// WHILE SORTED DESCENDING and on a row in the middle, because the bug this guards against
    /// (deleting by grid position instead of by region) only shows up when the display order and
    /// the stored order disagree.
    /// </summary>
    private static void ProbeRegionAddAndDelete(string outDir, List<string> report)
    {
        // Every verdict this probe owes, so an exception part-way through FAILS the outstanding
        // ones instead of quietly leaving them out of the summary (a probe that is missing reads
        // like a probe that was never written).
        string[] owed = ["region add", "region delete sorted", "region delete declined"];
        var answered = new HashSet<string>();

        void Say(string what, string verdict, string detail)
        {
            answered.Add(what);
            Record(report, what, verdict, detail);
        }

        RegionListWindow? window = null;
        try
        {
            var (w, vm, provider) = OpenRegionList();
            window = w;

            // ---- add
            var beforeAdd = vm.RegionCount;
            var addButton = FindByTag<Button>(window, "add-button");
            var addPoint = addButton == null ? null : CenterInWindow(addButton, window);
            if (addPoint == null)
            {
                Say("region add", "HARNESS-FAIL", "the Add Region button has no on-screen position");
            }
            else
            {
                Click(window, addPoint.Value);
                Pump();
                var added = vm.SelectedRow;
                var addOk = vm.RegionCount == beforeAdd + 1 &&
                            added != null &&
                            added.RegionNameText == RegionListViewModel.DefaultRegionName &&
                            provider.Regions.Count == beforeAdd + 1;
                Console.WriteLine($"  clicked Add Region; {beforeAdd} -> {vm.RegionCount} regions, " +
                                  $"selected '{added?.RegionNameText ?? "<null>"}'");
                Say("region add", addOk ? "PASS" : "APP-FAIL",
                    $"{beforeAdd} -> {vm.RegionCount} regions, new row selected=" +
                    $"'{added?.RegionNameText ?? "<null>"}'");
            }

            // ---- delete, sorted descending, from the middle of the list
            vm.SortDescending = true;
            Pump();

            var doomedRow = vm.Rows[2];
            var doomed = doomedRow.UnderlyingRegion;
            var survivors = provider.Regions.Where(r => !ReferenceEquals(r, doomed)).ToList();
            vm.SelectedRow = doomedRow;
            Pump();

            var asked = new List<string>();
            window.ConfirmDelete = message =>
            {
                asked.Add(message);
                return Task.FromResult(true);
            };

            var deleteButton = FindByTag<Button>(window, "delete-button");
            var deletePoint = deleteButton == null ? null : CenterInWindow(deleteButton, window);
            if (deletePoint == null)
            {
                Say("region delete sorted", "HARNESS-FAIL",
                    "the Delete Region button has no on-screen position");
            }
            else
            {
                Click(window, deletePoint.Value);
                Pump();
                Pump();

                var wentAway = !provider.Regions.Any(r => ReferenceEquals(r, doomed));
                var othersKept = survivors.All(s => provider.Regions.Any(r => ReferenceEquals(r, s)));
                var deleteOk = asked.Count == 1 && wentAway && othersKept;

                Console.WriteLine($"  clicked Delete on '{doomedRow.RegionNameText}' (sorted descending); " +
                                  $"asked {asked.Count}x, region gone={wentAway}, others kept={othersKept}");
                Say("region delete sorted", deleteOk ? "PASS" : "APP-FAIL",
                    $"asked {asked.Count}x ('{(asked.Count > 0 ? asked[0] : "")}'), " +
                    $"'{doomedRow.RegionNameText}' removed={wentAway}, every other region kept={othersKept}");
            }

            Capture(window, Path.Combine(outDir, "regionlist-after-add-delete.png"),
                $"region list: after Add Region + a confirmed Delete while sorted descending " +
                $"({vm.RegionCount} regions)");

            // ---- delete, declined: the question is asked and nothing happens
            var spared = vm.Rows[1];
            var sparedRegion = spared.UnderlyingRegion;
            vm.SelectedRow = spared;
            Pump();

            var declined = 0;
            window.ConfirmDelete = _ =>
            {
                declined++;
                return Task.FromResult(false);
            };

            var countBefore = vm.RegionCount;
            var declinePoint = CenterInWindow(FindByTag<Button>(window, "delete-button")!, window);
            if (declinePoint == null)
            {
                Say("region delete declined", "HARNESS-FAIL",
                    "the Delete Region button has no on-screen position");
            }
            else
            {
                Click(window, declinePoint.Value);
                Pump();
                Pump();

                var stillThere = provider.Regions.Any(r => ReferenceEquals(r, sparedRegion));
                var declineOk = declined == 1 && stillThere && vm.RegionCount == countBefore;
                Console.WriteLine($"  clicked Delete and said no; asked {declined}x, " +
                                  $"'{spared.RegionNameText}' still present={stillThere}");
                Say("region delete declined", declineOk ? "PASS" : "APP-FAIL",
                    $"asked {declined}x, region kept={stillThere}, count {countBefore} -> {vm.RegionCount}");
            }
        }
        catch (Exception ex)
        {
            foreach (var what in owed.Where(w => !answered.Contains(w)))
                Record(report, what, "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    /// <summary>
    /// The problem panel reports what no single row can: two asset regions covering the same
    /// bytes. It must appear when the overlap does and go away when it is fixed.
    /// </summary>
    private static void ProbeRegionProblemPanel(string outDir, List<string> report)
    {
        RegionListWindow? window = null;
        try
        {
            var (w, vm, _) = OpenRegionList(SeedOverlappingRegions);
            window = w;

            var list = FindByTag<ListBox>(window, "problems-list");
            var toggle = FindByTag<Button>(window, "problems-toggle");
            var shown = (list?.ItemsSource as IEnumerable<string>)?.ToList() ?? [];

            Console.WriteLine($"  problems: {vm.Problems.Count} in the ViewModel, " +
                              $"{shown.Count} in the panel, header='{toggle?.Content}'");
            foreach (var line in shown)
                Console.WriteLine($"    {line}");

            Capture(window, Path.Combine(outDir, "regionlist-problem-panel.png"),
                $"region list: two overlapping asset regions - problem panel shows " +
                $"{shown.Count} entry/entries");

            var listedOk = vm.Problems.Count == 1 &&
                           shown.Count == 1 &&
                           shown[0].StartsWith("Error: ", StringComparison.Ordinal) &&
                           shown[0].Contains("overlap", StringComparison.OrdinalIgnoreCase) &&
                           (toggle?.Content as string ?? "").Contains("Problems (1)", StringComparison.Ordinal);

            // fix it: move the first blob's end below the second blob's start.
            var blobA = vm.Rows.First(r => r.RegionNameText == "sprite_blob_a");
            var result = vm.CommitField(blobA, RegionField.End, "C1003F");
            Pump();

            var afterFix = (FindByTag<ListBox>(window, "problems-list")?.ItemsSource as IEnumerable<string>)
                           ?.ToList() ?? [];
            var afterToggle = FindByTag<Button>(window, "problems-toggle")?.Content as string ?? "";
            var clearedOk = result.IsValid && vm.Problems.Count == 0 && afterFix.Count == 0 &&
                            afterToggle.Contains("Problems (0)", StringComparison.Ordinal);

            Console.WriteLine($"  moved sprite_blob_a's end to C1003F; problems now " +
                              $"{vm.Problems.Count}, panel '{afterToggle}'");
            Record(report, "region problem panel", listedOk && clearedOk ? "PASS" : "APP-FAIL",
                $"overlap listed={listedOk} ({shown.Count} line(s)), cleared after the fix={clearedOk} " +
                $"({afterFix.Count} line(s), header '{afterToggle}')");
        }
        catch (Exception ex)
        {
            Record(report, "region problem panel", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    // ------------------------------------------------------------------ region details pane

    /// <summary>
    /// The mandatory anti-echo check, on the pane's most dangerous box. Start, End and Length are
    /// three views of one range, so any path that feeds a box's own text back to the ViewModel
    /// re-derives the other two -- and their answer need not be the text on screen. The address
    /// typed is a HiROM MIRROR BANK ($40:0200 is the same ROM byte as $C0:0200), so a round trip
    /// through the model would be VISIBLE rather than silent.
    ///
    /// Then the same box is used for the one rewrite that IS wanted: a pasted label is
    /// canonicalised to its address on commit, and that has to reach the screen.
    /// </summary>
    private static void ProbeRegionDetailTyping(List<string> report)
    {
        string[] owed = ["region detail typing", "region detail start commit"];
        var answered = new HashSet<string>();

        void Say(string what, string verdict, string detail)
        {
            answered.Add(what);
            Record(report, what, verdict, detail);
        }

        RegionListWindow? window = null;
        try
        {
            var (w, vm, _) = OpenRegionList();
            window = w;

            var row = vm.Rows.First(r => r.RegionNameText == "bank_C0_code");
            vm.SelectedRow = row;
            Pump();

            var startBox = FindByTag<TextBox>(window, "details-start");
            var endBox = FindByTag<TextBox>(window, "details-end");
            var lengthBox = FindByTag<TextBox>(window, "details-length");
            if (startBox == null || endBox == null || lengthBox == null)
            {
                Say("region detail typing", "HARNESS-FAIL",
                    $"pane boxes missing (start={startBox != null}, end={endBox != null}, " +
                    $"length={lengthBox != null})");
                return;
            }

            const string typed = "400200";
            var firstDivergence = TypeCharByChar(window, startBox, typed,
                _ => $"end box '{endBox.Text}', length box '{lengthBox.Text}'");
            if (firstDivergence == HarnessCouldNotType)
            {
                Say("region detail typing", "HARNESS-FAIL", "the Start box has no on-screen position");
                return;
            }

            var keptTyping = firstDivergence == null;
            Say("region detail typing", keptTyping ? "PASS" : "APP-FAIL",
                keptTyping
                    ? $"the Start box held every prefix of '{typed}' while it had the caret"
                    : firstDivergence!);

            // Enter commits. The commit is posted past the key event (so it cannot mutate the row
            // collection inside the grid's own bookkeeping), hence the second pump.
            PressEnter(window);

            var region = row.UnderlyingRegion;
            var mirrorOk = region.StartSnesAddress == 0x400200 && (startBox.Text ?? "") == "400200";
            var afterMirror = $"start=0x{region.StartSnesAddress:X6}, box='{startBox.Text}'";

            TypeIntoBox(window, startBox, "CODE_C012AB");
            PressEnter(window);

            var labelOk = region.StartSnesAddress == 0xC012AB && (startBox.Text ?? "") == "C012AB";
            Console.WriteLine($"  mirror-bank commit: {afterMirror}");
            Console.WriteLine($"  pasted label commit: start=0x{region.StartSnesAddress:X6}, " +
                              $"box='{startBox.Text}', length box '{lengthBox.Text}'");
            Say("region detail start commit", mirrorOk && labelOk ? "PASS" : "APP-FAIL",
                $"mirror bank -> {afterMirror}; pasted 'CODE_C012AB' -> " +
                $"start=0x{region.StartSnesAddress:X6}, box='{startBox.Text}'");
        }
        catch (Exception ex)
        {
            foreach (var what in owed.Where(w => !answered.Contains(w)))
                Record(report, what, "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    /// <summary>
    /// The four asset fields: the free-form JSON box has to survive being typed into one key at a
    /// time (every prefix of a JSON object is invalid JSON, so anything that validated as it went
    /// would fight the user), and the Export Type combo has to enable and disable the group
    /// WITHOUT the stored values going anywhere.
    /// </summary>
    private static void ProbeRegionDetailAssetFields(string outDir, List<string> report)
    {
        string[] owed = ["region detail json typing", "region detail asset fields toggle"];
        var answered = new HashSet<string>();

        void Say(string what, string verdict, string detail)
        {
            answered.Add(what);
            Record(report, what, verdict, detail);
        }

        RegionListWindow? window = null;
        try
        {
            var (w, vm, _) = OpenRegionList();
            window = w;

            var asset = vm.Rows.First(r => r.RegionNameText == "title_gfx");
            vm.SelectedRow = asset;
            Pump();

            Capture(window, Path.Combine(outDir, "regionlist-detail-asset.png"),
                "region list: details pane editing an ASSET region (title_gfx) - every field " +
                "editable, the four asset fields and the free-form JSON box enabled");

            var jsonBox = FindByTag<TextBox>(window, "details-assetoptions");
            var typeBox = FindByTag<TextBox>(window, "details-assettype");
            var assetPanel = FindByTag<Border>(window, "details-asset-fields");
            var combo = FindByTag<ComboBox>(window, "details-exporttype");
            if (jsonBox == null || typeBox == null || assetPanel == null || combo == null)
            {
                Say("region detail json typing", "HARNESS-FAIL",
                    $"asset widgets missing (json={jsonBox != null}, type={typeBox != null}, " +
                    $"panel={assetPanel != null}, combo={combo != null})");
                return;
            }

            const string json = "{\"cell_h\": 12}";
            var divergence = TypeCharByChar(window, jsonBox, json,
                _ => $"asset type box '{typeBox.Text}'");
            if (divergence == HarnessCouldNotType)
            {
                Say("region detail json typing", "HARNESS-FAIL",
                    "the Asset Options box has no on-screen position");
                return;
            }

            PressEnter(window);

            var stored = asset.UnderlyingRegion.AssetOptions;
            var jsonOk = divergence == null && stored == json && !asset.HasError;
            Console.WriteLine($"  JSON box typed key-by-key; region.AssetOptions='{stored}', " +
                              $"box='{jsonBox.Text}', row.HasError={asset.HasError}");
            Say("region detail json typing", jsonOk ? "PASS" : "APP-FAIL",
                divergence != null
                    ? divergence
                    : $"AssetOptions='{stored}' (wanted '{json}'), box='{jsonBox.Text}', " +
                      $"HasError={asset.HasError}");

            // ---- Export Type drives the asset group. Values must survive being greyed out.
            var enabledAsAsset = assetPanel.IsEnabled;
            combo.SelectedItem = nameof(RegionExportType.Assembly);
            Pump();
            Pump();

            var enabledAsAssembly = assetPanel.IsEnabled;
            var disabledOk = !enabledAsAssembly &&
                             asset.UnderlyingRegion.ExportType == RegionExportType.Assembly;
            var survivedType = asset.AssetTypeText;
            var survivedJson = asset.AssetOptionsText;

            // captured HERE, while the region really is an Assembly one and its asset descriptors
            // are still filled in: the point of greying rather than hiding is that you can see the
            // feature exists and see that nothing was thrown away.
            Capture(window, Path.Combine(outDir, "regionlist-detail-assembly.png"),
                "region list: details pane on an ASSEMBLY region - the asset fields are greyed " +
                "out rather than hidden, and still hold the values typed while it was an Asset");

            combo.SelectedItem = nameof(RegionExportType.Asset);
            Pump();
            Pump();

            var reEnabledOk = assetPanel.IsEnabled &&
                              asset.UnderlyingRegion.ExportType == RegionExportType.Asset &&
                              asset.AssetTypeText == "gfx.snes.4bpp" &&
                              asset.AssetOptionsText == json;

            Console.WriteLine($"  export type Asset->Assembly->Asset; asset group enabled " +
                              $"{enabledAsAsset} -> {enabledAsAssembly} -> {assetPanel.IsEnabled}; " +
                              $"values while disabled: type='{survivedType}' options='{survivedJson}'");
            Say("region detail asset fields toggle",
                enabledAsAsset && disabledOk && reEnabledOk ? "PASS" : "APP-FAIL",
                $"enabled as Asset={enabledAsAsset}, disabled as Assembly={disabledOk} " +
                $"(values kept: type='{survivedType}', options='{survivedJson}'), " +
                $"re-enabled and restored={reEnabledOk}");
        }
        catch (Exception ex)
        {
            foreach (var what in owed.Where(w => !answered.Contains(w)))
                Record(report, what, "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    /// <summary>
    /// Validation seen from the pane: a length the asset codec cannot use is refused (the model
    /// does not move, the typed text stays on screen, the field wears the marker), and the
    /// smallest legal region -- one byte, end == start -- commits.
    /// </summary>
    private static void ProbeRegionDetailValidation(string outDir, List<string> report)
    {
        string[] owed = ["region detail refuses", "region detail length 1"];
        var answered = new HashSet<string>();

        void Say(string what, string verdict, string detail)
        {
            answered.Add(what);
            Record(report, what, verdict, detail);
        }

        RegionListWindow? window = null;
        try
        {
            var (w, vm, _) = OpenRegionList();
            window = w;

            // BRR audio: the stream is 9-byte blocks, so 0x10 bytes is not a length it can have.
            var brr = vm.Rows.First(r => r.RegionNameText == "intro_song");
            vm.SelectedRow = brr;
            Pump();

            var lengthBox = FindByTag<TextBox>(window, "details-length");
            var lengthLabel = FindByTag<TextBlock>(window, "details-length-label");
            if (lengthBox == null)
            {
                Say("region detail refuses", "HARNESS-FAIL", "the Length box was not found");
                return;
            }

            var endBefore = brr.UnderlyingRegion.EndSnesAddress;
            TypeIntoBox(window, lengthBox, "10");
            PressEnter(window);

            var refusedOk = brr.UnderlyingRegion.EndSnesAddress == endBefore &&
                            brr.HasError &&
                            (lengthBox.Text ?? "") == "10" &&
                            brr.HasPendingTextFor(RegionField.Length) &&
                            vm.StatusText.Length > 0 &&
                            (lengthLabel?.Text ?? "").StartsWith('⚠');

            Console.WriteLine($"  length '10' on a BRR region: end 0x{endBefore:X6} -> " +
                              $"0x{brr.UnderlyingRegion.EndSnesAddress:X6}, HasError={brr.HasError}, " +
                              $"box='{lengthBox.Text}', label='{lengthLabel?.Text}'");
            Console.WriteLine($"  status: {vm.StatusText}");

            Capture(window, Path.Combine(outDir, "regionlist-detail-error.png"),
                "region list: details pane showing a REFUSED edit - the typed length is still in " +
                "the box, the field and the row are marked, and the region never moved");

            Say("region detail refuses", refusedOk ? "PASS" : "APP-FAIL",
                $"end unchanged={brr.UnderlyingRegion.EndSnesAddress == endBefore}, " +
                $"HasError={brr.HasError}, box kept='{lengthBox.Text}', " +
                $"field marked='{lengthLabel?.Text}', status='{vm.StatusText}'");

            // ---- the smallest legal region. The end address is inclusive, so length 1 means
            // end == start -- the case the old grid refused as "zero-length".
            var plain = vm.Rows.First(r => r.RegionNameText == "sram_notes");
            vm.SelectedRow = plain;
            Pump();

            TypeIntoBox(window, lengthBox, "1");
            PressEnter(window);

            var region = plain.UnderlyingRegion;
            var oneByteOk = region.EndSnesAddress == region.StartSnesAddress &&
                            !plain.HasError &&
                            plain.LengthText == "1";
            Console.WriteLine($"  length 1: start=0x{region.StartSnesAddress:X6} " +
                              $"end=0x{region.EndSnesAddress:X6} lengthText='{plain.LengthText}' " +
                              $"HasError={plain.HasError}");
            Say("region detail length 1", oneByteOk ? "PASS" : "APP-FAIL",
                $"start=0x{region.StartSnesAddress:X6}, end=0x{region.EndSnesAddress:X6}, " +
                $"LengthText='{plain.LengthText}', HasError={plain.HasError}");
        }
        catch (Exception ex)
        {
            foreach (var what in owed.Where(w => !answered.Contains(w)))
                Record(report, what, "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    /// <summary>
    /// The two gestures that finish an edit without pressing Enter.
    ///
    /// LOSING FOCUS is the one people actually use -- type, then click the next field -- and it
    /// has to commit. ESCAPE is the way back out of a value the model refused: the field stops
    /// showing the refused text, its marker goes with it, nothing is written, and blurring
    /// afterwards must NOT quietly re-offer the value the box was put back to.
    /// </summary>
    private static void ProbeRegionDetailLostFocusAndEscape(List<string> report)
    {
        string[] owed = ["region detail lostfocus commit", "region detail escape reverts"];
        var answered = new HashSet<string>();

        void Say(string what, string verdict, string detail)
        {
            answered.Add(what);
            Record(report, what, verdict, detail);
        }

        RegionListWindow? window = null;
        try
        {
            var (w, vm, _) = OpenRegionList();
            window = w;

            var nameBox = FindByTag<TextBox>(window, "details-name");
            var priorityBox = FindByTag<TextBox>(window, "details-priority");
            var lengthBox = FindByTag<TextBox>(window, "details-length");
            var nameLabel = FindByTag<TextBlock>(window, "details-name-label");
            var lengthLabel = FindByTag<TextBlock>(window, "details-length-label");
            if (nameBox == null || priorityBox == null || lengthBox == null)
            {
                Say("region detail lostfocus commit", "HARNESS-FAIL",
                    $"pane boxes missing (name={nameBox != null}, priority={priorityBox != null}, " +
                    $"length={lengthBox != null})");
                return;
            }

            // ---- commit by leaving the field, no Enter anywhere.
            var row = vm.Rows.First(r => r.RegionNameText == "battle_scratch");
            vm.SelectedRow = row;
            Pump();

            const string renamed = "committed_by_leaving";
            TypeIntoBox(window, nameBox, renamed);

            var priorityPoint = CenterInWindow(priorityBox, window);
            if (priorityPoint == null)
            {
                Say("region detail lostfocus commit", "HARNESS-FAIL",
                    "the Priority box has no on-screen position");
                return;
            }

            Click(window, priorityPoint.Value);
            Pump();
            Pump();

            var lostFocusOk = row.UnderlyingRegion.RegionName == renamed &&
                              !row.HasError &&
                              (nameLabel?.Text ?? "") == "Region Name";
            Console.WriteLine($"  typed in Region Name then clicked Priority: model is now " +
                              $"'{row.UnderlyingRegion.RegionName}', HasError={row.HasError}, " +
                              $"label='{nameLabel?.Text}'");
            Say("region detail lostfocus commit", lostFocusOk ? "PASS" : "APP-FAIL",
                $"model '{row.UnderlyingRegion.RegionName}' (wanted '{renamed}'), " +
                $"HasError={row.HasError}, name label='{nameLabel?.Text}'");

            // ---- refuse something, then back out of it with Escape.
            var brr = vm.Rows.First(r => r.RegionNameText == "intro_song");
            vm.SelectedRow = brr;
            Pump();

            var endBefore = brr.UnderlyingRegion.EndSnesAddress;
            var goodLength = brr.LengthText;
            TypeIntoBox(window, lengthBox, "10");
            PressEnter(window);

            var refusedFirst = brr.HasError && (lengthBox.Text ?? "") == "10";

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Pump();
            Pump();

            var revertedOk = (lengthBox.Text ?? "") == goodLength &&
                             !brr.HasPendingTextFor(RegionField.Length) &&
                             !brr.HasError &&
                             (lengthLabel?.Text ?? "") == "Length (hex)" &&
                             brr.UnderlyingRegion.EndSnesAddress == endBefore;

            // and leaving the field afterwards must not re-offer what Escape put back.
            Click(window, priorityPoint.Value);
            Pump();
            Pump();

            var stayedClean = !brr.HasError && brr.UnderlyingRegion.EndSnesAddress == endBefore;

            Console.WriteLine($"  refused '10' ({refusedFirst}), Escape -> box '{lengthBox.Text}' " +
                              $"(wanted '{goodLength}'), HasError={brr.HasError}, " +
                              $"label='{lengthLabel?.Text}'; after blurring HasError={brr.HasError}");
            Say("region detail escape reverts",
                refusedFirst && revertedOk && stayedClean ? "PASS" : "APP-FAIL",
                $"refused first={refusedFirst}, box back to '{lengthBox.Text}', " +
                $"pending cleared={!brr.HasPendingTextFor(RegionField.Length)}, " +
                $"marker cleared={!brr.HasError} (label '{lengthLabel?.Text}'), " +
                $"end still 0x{brr.UnderlyingRegion.EndSnesAddress:X6}, clean after blur={stayedClean}");
        }
        catch (Exception ex)
        {
            foreach (var what in owed.Where(w => !answered.Contains(w)))
                Record(report, what, "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    /// <summary>
    /// A refused edit to a CLOSED-VALUE field. A checkbox cannot display a value the model
    /// rejected, so it has to snap back to what the region holds -- and leave NO marker behind,
    /// because there is nothing on screen that the model did not accept. The retry after fixing
    /// the real problem then has to go through: an earlier version of the WinForms grid swallowed
    /// it, because it compared the new attempt against text the refusal had parked.
    /// </summary>
    private static void ProbeRegionDetailClosedValueSnapBack(List<string> report)
    {
        RegionListWindow? window = null;
        try
        {
            var (w, vm, _) = OpenRegionList(SeedCrossBankRegions);
            window = w;

            var row = vm.Rows.First(r => r.RegionNameText == "spans_two_banks");
            vm.SelectedRow = row;
            Pump();

            var check = FindByTag<CheckBox>(window, "details-sepfile");
            var endBox = FindByTag<TextBox>(window, "details-end");
            var sepLabel = FindByTag<TextBlock>(window, "details-sepfile-label");
            var point = check == null ? null : CenterInWindow(check, window);
            if (check == null || endBox == null || point == null)
            {
                Record(report, "region detail closed value snap back", "HARNESS-FAIL",
                    $"widgets missing (check={check != null}, end={endBox != null}, " +
                    $"positioned={point != null})");
                return;
            }

            Click(window, point.Value);
            Pump();
            Pump();

            var refusedOk = check.IsChecked != true &&
                            !row.UnderlyingRegion.ExportSeparateFile &&
                            !row.HasError &&
                            !row.HasPendingTextFor(RegionField.ExportSeparateFile) &&
                            (sepLabel?.Text ?? "") == "Separate File" &&
                            vm.StatusText.Contains("same bank", StringComparison.OrdinalIgnoreCase);

            Console.WriteLine($"  ticked Separate File on a cross-bank region: checkbox now " +
                              $"{check.IsChecked}, model={row.UnderlyingRegion.ExportSeparateFile}, " +
                              $"HasError={row.HasError}, label='{sepLabel?.Text}'");
            Console.WriteLine($"  status: {vm.StatusText}");

            // fix the range so the region lives in one bank, then tick again.
            TypeIntoBox(window, endBox, "C0FFFF");
            PressEnter(window);

            Click(window, point.Value);
            Pump();
            Pump();

            var retryOk = check.IsChecked == true &&
                          row.UnderlyingRegion.ExportSeparateFile &&
                          !row.HasError;

            Console.WriteLine($"  fixed the end to C0FFFF and ticked again: checkbox " +
                              $"{check.IsChecked}, model={row.UnderlyingRegion.ExportSeparateFile}");
            Record(report, "region detail closed value snap back",
                refusedOk && retryOk ? "PASS" : "APP-FAIL",
                $"refused: checkbox snapped back={check.IsChecked != true || retryOk}, " +
                $"no marker={!row.HasError || retryOk}, status named the bank rule=" +
                $"{refusedOk}; retry after the fix committed={retryOk}");
        }
        catch (Exception ex)
        {
            Record(report, "region detail closed value snap back", "EXCEPTION",
                ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    /// <summary>
    /// The two halves of master/detail: a committed pane edit has to repaint the master row (the
    /// grid's cells are one-way bindings over the row's own change notification), and picking a
    /// different row has to swap the pane WITHOUT committing whatever was half-typed -- text a
    /// region never accepted belongs to the region it was typed against, not to the next one.
    /// </summary>
    private static void ProbeRegionDetailMasterSync(List<string> report)
    {
        string[] owed = ["region detail master repaint", "region detail row swap"];
        var answered = new HashSet<string>();

        void Say(string what, string verdict, string detail)
        {
            answered.Add(what);
            Record(report, what, verdict, detail);
        }

        RegionListWindow? window = null;
        try
        {
            var (w, vm, _) = OpenRegionList();
            window = w;

            var row = vm.Rows.First(r => r.RegionNameText == "battle_scratch");
            vm.SelectedRow = row;
            Pump();

            var nameBox = FindByTag<TextBox>(window, "details-name");
            if (nameBox == null)
            {
                Say("region detail master repaint", "HARNESS-FAIL", "the Region Name box was not found");
                return;
            }

            const string renamed = "battle_scratch_renamed";
            var masterBefore = MasterCellText(window, row, RegionNameColumnIndex);
            TypeIntoBox(window, nameBox, renamed);
            PressEnter(window);

            var masterAfter = MasterCellText(window, row, RegionNameColumnIndex);
            var repaintOk = row.UnderlyingRegion.RegionName == renamed && masterAfter == renamed;
            Console.WriteLine($"  pane rename '{masterBefore}' -> model '{row.UnderlyingRegion.RegionName}'; " +
                              $"master grid cell now '{masterAfter}'");
            Say("region detail master repaint", repaintOk ? "PASS" : "APP-FAIL",
                $"master cell '{masterBefore}' -> '{masterAfter}' (model " +
                $"'{row.UnderlyingRegion.RegionName}')");

            // ---- half-type, then click a different row.
            var other = vm.Rows.First(r => r.RegionNameText == "sram_notes");
            var editedBefore = row.UnderlyingRegion.RegionName;
            var otherBefore = other.UnderlyingRegion.RegionName;

            TypeIntoBox(window, nameBox, "half_typed_never_committed");
            if (!ClickRegionRow(window, other))
            {
                Say("region detail row swap", "HARNESS-FAIL", "the target row has no on-screen position");
                return;
            }

            Pump();

            var swapOk = row.UnderlyingRegion.RegionName == editedBefore &&
                         other.UnderlyingRegion.RegionName == otherBefore &&
                         ReferenceEquals(vm.SelectedRow, other) &&
                         (nameBox.Text ?? "") == other.RegionNameText;
            Console.WriteLine($"  half-typed then clicked '{other.RegionNameText}': previous row is " +
                              $"still '{row.UnderlyingRegion.RegionName}', clicked row is still " +
                              $"'{other.UnderlyingRegion.RegionName}', pane box now '{nameBox.Text}'");
            Say("region detail row swap", swapOk ? "PASS" : "APP-FAIL",
                $"previous row '{row.UnderlyingRegion.RegionName}' (was '{editedBefore}'), " +
                $"clicked row '{other.UnderlyingRegion.RegionName}' (was '{otherBefore}'), " +
                $"pane shows '{nameBox.Text}'");
        }
        catch (Exception ex)
        {
            foreach (var what in owed.Where(w => !answered.Contains(w)))
                Record(report, what, "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    /// <summary>Delete on the grid is the toolbar button's gesture: the same one confirmation and
    /// the same by-identity removal, not a second delete path.</summary>
    private static void ProbeRegionDeleteKey(List<string> report)
    {
        RegionListWindow? window = null;
        try
        {
            var (w, vm, provider) = OpenRegionList();
            window = w;

            var doomedRow = vm.Rows[2];
            var doomed = doomedRow.UnderlyingRegion;
            var survivors = provider.Regions.Where(r => !ReferenceEquals(r, doomed)).ToList();

            if (!ClickRegionRow(window, doomedRow))
            {
                Record(report, "region del key", "HARNESS-FAIL", "the target row has no on-screen position");
                return;
            }

            var asked = new List<string>();
            window.ConfirmDelete = message =>
            {
                asked.Add(message);
                return Task.FromResult(true);
            };

            window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
            window.KeyRelease(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
            Pump();
            Pump();

            var wentAway = !provider.Regions.Any(r => ReferenceEquals(r, doomed));
            var othersKept = survivors.All(s => provider.Regions.Any(r => ReferenceEquals(r, s)));
            var ok = asked.Count == 1 && wentAway && othersKept;

            Console.WriteLine($"  Del on '{doomedRow.RegionNameText}': asked {asked.Count}x, " +
                              $"gone={wentAway}, others kept={othersKept}");
            Record(report, "region del key", ok ? "PASS" : "APP-FAIL",
                $"asked {asked.Count}x, '{doomedRow.RegionNameText}' removed={wentAway}, " +
                $"every other region kept={othersKept}");
        }
        catch (Exception ex)
        {
            Record(report, "region del key", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    /// <summary>Closing the window hides it. The instance and its ViewModel are the app's for its
    /// whole lifetime, so a close that really closed would take the region editor away until the
    /// project was reopened.</summary>
    private static void ProbeRegionCloseHides(List<string> report)
    {
        RegionListWindow? window = null;
        try
        {
            var (w, vm, _) = OpenRegionList();
            window = w;

            var visibleBefore = window.IsVisible;
            window.Close();
            Pump();
            var hidden = !window.IsVisible;

            window.Show();
            Pump();

            var grid = FindByTag<DataGrid>(window, "region-grid");
            var reopenedOk = window.IsVisible &&
                             ReferenceEquals(window.DataContext, vm) &&
                             grid?.ItemsSource != null;

            Console.WriteLine($"  visible {visibleBefore} -> closed -> visible {!hidden} -> " +
                              $"shown again {window.IsVisible}, still bound={reopenedOk}");
            Record(report, "region close hides", visibleBefore && hidden && reopenedOk ? "PASS" : "APP-FAIL",
                $"visible before={visibleBefore}, hidden after Close={hidden}, " +
                $"re-shown and still bound={reopenedOk}");
        }
        catch (Exception ex)
        {
            Record(report, "region close hides", "EXCEPTION", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            window?.Close();
            Pump();
        }
    }

    /// <summary>Position of the Region Name column in the master grid (see ExpectedRegionColumns).</summary>
    private const int RegionNameColumnIndex = 3;

    /// <summary>
    /// What the master grid is DISPLAYING for one row's column, read out of the realized cell
    /// rather than off the ViewModel -- the point is whether the grid repainted, so asking the
    /// ViewModel again would answer the wrong question.
    /// </summary>
    private static string MasterCellText(RegionListWindow window, IRegionRowViewModel row, int columnIndex)
    {
        var container = window.GetVisualDescendants().OfType<DataGridRow>()
            .FirstOrDefault(r => ReferenceEquals(r.DataContext, row));
        var cells = container?.GetVisualDescendants().OfType<DataGridCell>().ToList();
        if (cells == null || columnIndex >= cells.Count)
            return "";

        return cells[columnIndex].GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text ?? "";
    }

    /// <summary>Enter commits the focused pane editor. The commit is POSTED past the key event,
    /// so the dispatcher has to be run again before the result can be read.</summary>
    private static void PressEnter(Window window)
    {
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Pump();
        Pump();
    }

    private static bool ClickRegionRow(RegionListWindow window, IRegionRowViewModel row)
    {
        var container = window.GetVisualDescendants().OfType<DataGridRow>()
            .FirstOrDefault(r => ReferenceEquals(r.DataContext, row));
        var cell = container?.GetVisualDescendants().OfType<DataGridCell>().FirstOrDefault();
        var point = cell == null ? null : CenterInWindow(cell, window);
        if (point == null)
            return false;

        Click(window, point.Value);
        Pump();
        return true;
    }

    private static DataGridColumnHeader? FindColumnHeader(Window window, string caption) =>
        window.GetVisualDescendants().OfType<DataGridColumnHeader>()
            .FirstOrDefault(h => StripSortArrow(h.Content as string ?? "") == caption);

    private static string StripSortArrow(string header) =>
        header.Replace("▲", "").Replace("▼", "").Trim();

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

    private static void Click(Window window, Point p)
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
