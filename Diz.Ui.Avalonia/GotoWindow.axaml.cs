using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Diz.Ui.ViewModels.Goto;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia "goto" window: a passive view over the same GotoViewModel the WinForms window
/// hosts. Everything that decides where "there" is -- address conversion, hex/decimal parsing,
/// label stripping, validation -- lives in the ViewModel; this file is widget wiring only, and
/// the window never navigates. The caller reads ResultPcOffset off the ViewModel once
/// <see cref="Completion"/> reports true.
///
/// Layout is in GotoWindow.axaml; the x:Name fields and InitializeComponent come from the
/// Avalonia XAML source generator -- never hand-write them, or the generator suppresses its
/// own version and every named control is null at runtime.
///
/// Wiring is explicit rather than declarative binding, for the same reason the WinForms host
/// hand-wires: the two address boxes are text projections the ViewModel deliberately refuses to
/// re-notify while they hold the caret, so a binding would fight the user's typing.
///
/// Unlike a modal dialog there is no blocking call: the window completes a task when the user
/// finishes. Closing it any other way (the X, Escape) counts as cancel.
/// </summary>
internal sealed partial class GotoWindow : Window
{
    private readonly TaskCompletionSource<bool> completion = new();

    private GotoViewModel? vm;

    // which box's text is selected when the window opens, so typing replaces it. The flag
    // means what it says: true selects the SNES Address box.
    private bool initiallySelectSnesAddr = true;

    // true while widget values are being written FROM the ViewModel; the input handlers below
    // bail out then, so a ViewModel-driven refresh can't be mistaken for the user typing.
    // Only catches handlers that run inside the write itself -- TextChanged does not (see
    // PushText), so the text boxes need the value comparison there as well as this flag.
    private bool updatingWidgets;

    public GotoWindow()
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

    /// <param name="viewModel">Holds both address projections and decides which are valid.</param>
    /// <param name="selectSnesAddrInitially">
    /// True selects the SNES ADDRESS box, false the ROM FILE OFFSET box (matches IGotoView's
    /// contract after the 2026-07-26 un-inversion).
    /// </param>
    public void AttachViewModel(GotoViewModel viewModel, bool selectSnesAddrInitially = true)
    {
        DetachViewModel();

        vm = viewModel;
        initiallySelectSnesAddr = selectSnesAddrInitially;
        vm.PropertyChanged += Vm_PropertyChanged;

        RefreshAllWidgets();
    }

    public void DetachViewModel()
    {
        if (vm == null)
            return;

        vm.PropertyChanged -= Vm_PropertyChanged;
        vm = null;
    }

    /// <summary>
    /// Pre-select one box's text so the first keystroke replaces it. Done once the window is
    /// open, because focus has nowhere to go before that.
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        SelectInitialText();
    }

    private void SelectInitialText()
    {
        var box = initiallySelectSnesAddr ? SnesBox : PcBox;
        box.Focus();
        box.SelectAll();
    }

    // ------------------------------------------------------------------ ViewModel -> widgets

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (vm == null)
            return;

        switch (e.PropertyName)
        {
            case nameof(GotoViewModel.SnesText):
                WriteText(SnesBox, vm.SnesText);
                break;

            case nameof(GotoViewModel.PcText):
                WriteText(PcBox, vm.PcText);
                break;

            case nameof(GotoViewModel.UseHexadecimal):
                WriteWidgets(() =>
                {
                    RadioHex.IsChecked = vm.UseHexadecimal;
                    RadioDec.IsChecked = !vm.UseHexadecimal;
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
            SnesBox.Text = vm.SnesText;
            PcBox.Text = vm.PcText;
            RadioHex.IsChecked = vm.UseHexadecimal;
            RadioDec.IsChecked = !vm.UseHexadecimal;
        });

        RefreshValidation();
    }

    /// <summary>
    /// Go is available only while the ViewModel names a real destination, and the reason it
    /// doesn't is shown verbatim beneath the fields. The view never re-derives validity; it
    /// only displays the answer.
    /// </summary>
    private void RefreshValidation()
    {
        GoButton.IsEnabled = vm?.CanConfirm ?? false;
        ErrorText.Text = vm?.ValidationMessage ?? "";
    }

    // ------------------------------------------------------------------ widgets -> ViewModel

    private void SnesBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        PushText(SnesBox, vm?.SnesText, text => vm!.SnesText = text);

    private void PcBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        PushText(PcBox, vm?.PcText, text => vm!.PcText = text);

    // the radio pair reports through the button losing its check as well as the one gaining
    // it, so this one handler covers both directions.
    private void NumberBase_Changed(object? sender, RoutedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (vm != null)
                vm.UseHexadecimal = RadioHex.IsChecked == true;
        });

    private void GoButton_Click(object? sender, RoutedEventArgs e)
    {
        // guard: Go is disabled while invalid, but IsDefault means Enter can reach it too, so
        // the confirm path is gated here as well and not only by the button's enabled state.
        if (vm?.CanConfirm != true)
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
    private void PushToViewModel(Action push)
    {
        if (updatingWidgets)
            return;

        push();
    }

    /// <summary>
    /// Hand a box's text to the ViewModel -- unless the box is only reporting back what the
    /// ViewModel itself just put there.
    ///
    /// The updatingWidgets flag cannot decide that on its own. Avalonia raises TextChanged on a
    /// LATER dispatcher turn, not inside the Text setter, so by the time the event arrives the
    /// flag is down again and a write this window made is indistinguishable from a keystroke.
    /// Comparing the text instead does not depend on when the event fires.
    ///
    /// Letting an echo through is not harmless. A box holding the ViewModel's own value has
    /// nothing new to say, but assigning it re-derives the OTHER box from it, and that answer
    /// need not be the text on screen: HiROM ignores the top two bank bits, so $40:0200 and
    /// $C0:0200 are one ROM byte, and the offset converts back to the canonical bank. Round
    /// tripping it retypes the user's address under their caret, mid-word.
    /// </summary>
    private void PushText(TextBox box, string? viewModelText, Action<string> assign)
    {
        if (vm == null || updatingWidgets)
            return;

        var text = box.Text ?? "";
        if (string.Equals(text, viewModelText, StringComparison.Ordinal))
            return;

        assign(text);
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
    /// Push text into a box, INCLUDING the one being typed into. That is deliberate here: the
    /// ViewModel already withholds the notification for text it accepted unchanged, so the only
    /// time the edited box is written is when accepting rewrote it -- pasting the label
    /// "CODE_C012AB" leaves "C012AB" behind -- and that rewrite has to be visible.
    /// </summary>
    private void WriteText(TextBox textBox, string text) => WriteWidgets(() => textBox.Text = text);
}
