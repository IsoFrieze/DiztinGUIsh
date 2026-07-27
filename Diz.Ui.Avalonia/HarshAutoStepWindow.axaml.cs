using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Diz.Ui.ViewModels.HarshAutoStep;
using Diz.Ui.ViewModels.MarkMany;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia "harsh auto step" window: a passive view over the same HarshAutoStepViewModel
/// the WinForms window hosts. Everything that decides which bytes get decoded -- range math,
/// address conversion, hex/decimal parsing, validation -- lives in the ViewModel; this file is
/// widget wiring only, and the window never steps anything. The caller reads
/// BuildAutoStepHarshCommand() off the ViewModel once <see cref="Completion"/> reports true.
///
/// Layout is in HarshAutoStepWindow.axaml; the x:Name fields and InitializeComponent come from
/// the Avalonia XAML source generator -- never hand-write them, or the generator suppresses its
/// own version and every named control is null at runtime.
///
/// Wiring is explicit rather than declarative binding, for the same reason the WinForms host
/// hand-wires: the three range fields are text projections the ViewModel deliberately refuses to
/// re-notify while they hold the caret, so a binding would fight the user's typing.
///
/// Unlike a modal dialog there is no blocking call: the window completes a task when the user
/// finishes. Closing it any other way (the X, Escape) counts as cancel.
/// </summary>
internal sealed partial class HarshAutoStepWindow : Window
{
    private readonly TaskCompletionSource<bool> completion = new();

    private HarshAutoStepViewModel? vm;

    // true while widget values are being written FROM the ViewModel; the input handlers below
    // bail out then, so a ViewModel-driven refresh can't be mistaken for the user typing.
    // Only catches handlers that run inside the write itself -- TextChanged does not (see
    // PushText), so the text boxes need the value comparison there as well as this flag.
    private bool updatingWidgets;

    // the control the user is currently typing into. Text is never pushed back into it while
    // it holds the caret -- reformatting a field under the caret fights the user. Set only for
    // the duration of one push, so like updatingWidgets it does not reach a TextChanged raised
    // on a later dispatcher turn.
    private Control? controlBeingEdited;

    public HarshAutoStepWindow()
    {
        InitializeComponent();

        Closed += (_, _) =>
        {
            DetachViewModel();
            // closing without pressing Go is a cancel; a no-op if Go already answered.
            completion.TrySetResult(false);
        };
    }

    /// <summary>Completes when the user is done: true if they confirmed, false if they cancelled.</summary>
    public Task<bool> Completion => completion.Task;

    // ------------------------------------------------------------------ VM attach/detach

    /// <param name="viewModel">Holds the range, and decides whether it is worth stepping through.</param>
    public void AttachViewModel(HarshAutoStepViewModel viewModel)
    {
        DetachViewModel();

        vm = viewModel;
        // the range is the whole editable surface here, so it is the only thing to listen to.
        vm.Range.PropertyChanged += Range_PropertyChanged;

        RefreshAllWidgets();
    }

    public void DetachViewModel()
    {
        if (vm == null)
            return;

        vm.Range.PropertyChanged -= Range_PropertyChanged;
        vm = null;
    }

    // ------------------------------------------------------------------ ViewModel -> widgets

    private void Range_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (vm == null)
            return;

        switch (e.PropertyName)
        {
            case nameof(AddressRangeViewModel.StartText):
                WriteText(StartBox, vm.Range.StartText);
                break;

            case nameof(AddressRangeViewModel.EndText):
                WriteText(EndBox, vm.Range.EndText);
                break;

            case nameof(AddressRangeViewModel.CountText):
                WriteText(CountBox, vm.Range.CountText);
                break;

            case nameof(AddressRangeViewModel.UseHexadecimal):
                WriteWidgets(() =>
                {
                    RadioHex.IsChecked = vm.Range.UseHexadecimal;
                    RadioDec.IsChecked = !vm.Range.UseHexadecimal;
                });
                break;

            case nameof(AddressRangeViewModel.UseSnesAddresses):
                WriteWidgets(() =>
                {
                    RadioSnes.IsChecked = vm.Range.UseSnesAddresses;
                    RadioPc.IsChecked = !vm.Range.UseSnesAddresses;
                });
                break;
        }

        RefreshValidation();
    }

    private void RefreshAllWidgets()
    {
        if (vm == null)
            return;

        WriteWidgets(() =>
        {
            RadioSnes.IsChecked = vm.Range.UseSnesAddresses;
            RadioPc.IsChecked = !vm.Range.UseSnesAddresses;
            RadioHex.IsChecked = vm.Range.UseHexadecimal;
            RadioDec.IsChecked = !vm.Range.UseHexadecimal;

            StartBox.Text = vm.Range.StartText;
            EndBox.Text = vm.Range.EndText;
            CountBox.Text = vm.Range.CountText;
        });

        RefreshValidation();
    }

    /// <summary>
    /// Go is available only when the ViewModel can actually build a command, and the reason it
    /// can't is shown verbatim beneath the fields. The view never re-derives validity; it only
    /// displays the answer.
    /// </summary>
    private void RefreshValidation()
    {
        GoButton.IsEnabled = vm?.CanBuildAutoStepCommand ?? false;
        ErrorText.Text = vm?.ValidationMessage ?? "";
    }

    // ------------------------------------------------------------------ widgets -> ViewModel

    private void StartBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        PushText(StartBox, vm?.Range.StartText, text => vm!.Range.StartText = text);

    private void EndBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        PushText(EndBox, vm?.Range.EndText, text => vm!.Range.EndText = text);

    private void CountBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        PushText(CountBox, vm?.Range.CountText, text => vm!.Range.CountText = text);

    // each radio pair reports through the button that is losing its check as well as the one
    // gaining it, so one handler per pair covers both directions.
    private void AddressSpace_Changed(object? sender, RoutedEventArgs e) =>
        PushToViewModel(null, () =>
        {
            if (vm != null)
                vm.Range.UseSnesAddresses = RadioSnes.IsChecked == true;
        });

    private void NumberBase_Changed(object? sender, RoutedEventArgs e) =>
        PushToViewModel(null, () =>
        {
            if (vm != null)
                vm.Range.UseHexadecimal = RadioHex.IsChecked == true;
        });

    private void GoButton_Click(object? sender, RoutedEventArgs e)
    {
        // guard: Go is disabled while invalid, but IsDefault means Enter can reach it too, so
        // the confirm path is gated here as well and not only by the button's enabled state.
        if (vm?.CanBuildAutoStepCommand != true)
            return;

        completion.TrySetResult(true);
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        completion.TrySetResult(false);
        Close();
    }

    // ------------------------------------------------------------------ plumbing

    /// <summary>Run a user-input handler, unless the change came from the ViewModel in the first place.</summary>
    private void PushToViewModel(Control? sourceControl, Action push)
    {
        if (updatingWidgets)
            return;

        var previous = controlBeingEdited;
        controlBeingEdited = sourceControl;
        try
        {
            push();
        }
        finally
        {
            controlBeingEdited = previous;
        }
    }

    /// <summary>
    /// Hand a box's text to the ViewModel -- unless the box is only reporting back what the
    /// ViewModel itself just put there.
    ///
    /// Neither guard above can decide that on its own. Avalonia raises TextChanged on a LATER
    /// dispatcher turn, not inside the Text setter, so by the time the event arrives
    /// updatingWidgets is down again and controlBeingEdited is back to null: a write this window
    /// made is indistinguishable from a keystroke. Comparing the text instead does not depend on
    /// when the event fires.
    ///
    /// Letting an echo through is not harmless. A box holding the ViewModel's own value has
    /// nothing new to say, but assigning it re-derives the other range fields from it, and their
    /// answer need not be the text on screen: HiROM ignores the top two bank bits, so $40:0200
    /// and $C0:0200 are one ROM byte, and the offset converts back to the canonical bank. Round
    /// tripping it retypes the user's address under their caret, mid-word.
    /// </summary>
    private void PushText(TextBox box, string? viewModelText, Action<string> assign)
    {
        if (vm == null || updatingWidgets)
            return;

        var text = box.Text ?? "";
        if (string.Equals(text, viewModelText, StringComparison.Ordinal))
            return;

        PushToViewModel(box, () => assign(text));
    }

    /// <summary>Write widget state without the input handlers treating it as user input.</summary>
    private void WriteWidgets(Action write)
    {
        var previous = updatingWidgets;
        updatingWidgets = true;
        try
        {
            write();
        }
        finally
        {
            updatingWidgets = previous;
        }
    }

    /// <summary>
    /// Push text into a box, EXCEPT the one being typed into. Retyping a field under the user's
    /// caret fights every keystroke -- and that would happen constantly here, because a SNES
    /// address typed in a mirrored bank converts back to the canonical bank.
    /// </summary>
    private void WriteText(TextBox textBox, string text)
    {
        if (ReferenceEquals(textBox, controlBeingEdited))
            return;

        WriteWidgets(() => textBox.Text = text);
    }
}
