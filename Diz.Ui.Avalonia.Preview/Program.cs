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
