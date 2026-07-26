using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Diz.Core.commands;
using Diz.Core.Interfaces;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.Ui.ViewModels.MarkMany;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia "mark many" window: a passive view over the same MarkManyViewModel the
/// WinForms window hosts. Everything that decides what gets marked -- range math, address
/// conversion, hex/decimal parsing, validation, session memory -- lives in the ViewModel;
/// this file is widget wiring only, and the window never applies the command. The caller
/// reads BuildMarkCommand() off the ViewModel once <see cref="Completion"/> reports true.
///
/// Layout is in MarkManyWindow.axaml; the x:Name fields and InitializeComponent come from the
/// Avalonia XAML source generator -- never hand-write them, or the generator suppresses its
/// own version and every named control is null at runtime.
///
/// Wiring is explicit rather than declarative binding, for the same reasons the WinForms host
/// hand-wires: the range fields are text projections the ViewModel deliberately refuses to
/// re-notify while they hold the caret, the register value is an int that has to be formatted
/// in the currently selected number base, and combo indices are mapped through arrays so no
/// index arithmetic ever reaches the ViewModel.
///
/// Unlike a modal dialog there is no blocking call: the window completes a task when the user
/// finishes. Closing it any other way (the X, Escape) counts as cancel.
/// </summary>
internal sealed partial class MarkManyWindow : Window
{
    // combo contents. The display strings and the model values behind each index are kept in
    // lock-step arrays. "CPU architecture" is deliberately absent from the property combo: the
    // ViewModel and the applier both support marking it, but no menu ever selects it and the
    // window has never offered it. If it ever becomes relevant, add MarkManyProperty.CpuArch
    // and its display string to the two arrays below -- the architecture combo it selects is
    // already built and wired.
    private static readonly MarkCommand.MarkManyProperty[] PropertyComboValues =
    [
        MarkCommand.MarkManyProperty.Flag,
        MarkCommand.MarkManyProperty.DataBank,
        MarkCommand.MarkManyProperty.DirectPage,
        MarkCommand.MarkManyProperty.MFlag,
        MarkCommand.MarkManyProperty.XFlag,
    ];

    private static readonly string[] PropertyComboLabels =
        ["Flag", "Data Bank", "Direct Page", "M Flag", "X Flag"];

    private static readonly FlagType[] FlagComboValues =
    [
        FlagType.Unreached, FlagType.Opcode, FlagType.Operand, FlagType.Data8Bit,
        FlagType.Graphics, FlagType.Music, FlagType.Empty, FlagType.Data16Bit,
        FlagType.Pointer16Bit, FlagType.Data24Bit, FlagType.Pointer24Bit,
        FlagType.Data32Bit, FlagType.Pointer32Bit, FlagType.Text,
    ];

    private static readonly string[] FlagComboLabels =
    [
        "Unreached", "Opcode", "Operand", "Data (8-Bit)", "Graphics", "Music", "Empty",
        "Data (16-Bit)", "Pointer (16-Bit)", "Data (24-Bit)", "Pointer (24-Bit)",
        "Data (32-Bit)", "Pointer (32-Bit)", "Text",
    ];

    private static readonly Architecture[] ArchComboValues =
        [Architecture.Cpu65C816, Architecture.Apuspc700, Architecture.GpuSuperFx];

    private static readonly string[] ArchComboLabels =
        ["65C816 (CPU)", "SPC700 (APU)", "SuperFX (GPU)"];

    // MxCombo: index 0 = "16-Bit", index 1 = "8-Bit"
    private const int MxComboIndex16Bit = 0;
    private const int MxComboIndex8Bit = 1;

    private readonly TaskCompletionSource<bool> completion = new();

    private MarkManyViewModel<ISnesData>? vm;

    // true while widget values are being written FROM the ViewModel; the input handlers below
    // bail out then, so a ViewModel-driven refresh can't be mistaken for the user typing.
    // Only catches handlers that run inside the write itself -- TextChanged does not (see
    // PushText), so the text boxes need the value comparison there as well as this flag.
    private bool updatingWidgets;

    // the control the user is currently typing into. Text is never pushed back into it while
    // it holds the caret -- reformatting a field under the caret fights the user. (The range
    // ViewModel already withholds notifications for the field being edited; this covers the
    // register value box, which shares no such rule.) Set only for the duration of one push, so
    // like updatingWidgets it does not reach a TextChanged raised on a later dispatcher turn.
    private Control? controlBeingEdited;

    public MarkManyWindow()
    {
        InitializeComponent();

        PropertyCombo.ItemsSource = PropertyComboLabels;
        FlagCombo.ItemsSource = FlagComboLabels;
        ArchCombo.ItemsSource = ArchComboLabels;
        MxCombo.ItemsSource = new[] { "16-Bit", "8-Bit" };

        Closed += (_, _) =>
        {
            DetachViewModel();
            // closing without pressing OK is a cancel; a no-op if OK already answered.
            completion.TrySetResult(false);
        };
    }

    /// <summary>Completes when the user is done: true if they confirmed, false if they cancelled.</summary>
    public Task<bool> Completion => completion.Task;

    // ------------------------------------------------------------------ VM attach/detach

    public void AttachViewModel(MarkManyViewModel<ISnesData> viewModel)
    {
        DetachViewModel();

        vm = viewModel;
        vm.PropertyChanged += Vm_PropertyChanged;
        vm.Range.PropertyChanged += Range_PropertyChanged;

        RefreshAllWidgets();
    }

    public void DetachViewModel()
    {
        if (vm == null)
            return;

        vm.PropertyChanged -= Vm_PropertyChanged;
        vm.Range.PropertyChanged -= Range_PropertyChanged;
        vm = null;
    }

    // ------------------------------------------------------------------ ViewModel -> widgets

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (vm == null)
            return;

        switch (e.PropertyName)
        {
            case nameof(MarkManyViewModel<ISnesData>.SelectedProperty):
                WriteWidgets(() =>
                    PropertyCombo.SelectedIndex = Array.IndexOf(PropertyComboValues, vm.SelectedProperty));
                break;

            case nameof(MarkManyViewModel<ISnesData>.IsFlagValueUsed):
            case nameof(MarkManyViewModel<ISnesData>.IsRegisterValueUsed):
            case nameof(MarkManyViewModel<ISnesData>.IsRegisterWidthUsed):
            case nameof(MarkManyViewModel<ISnesData>.IsArchitectureValueUsed):
                RefreshValueWidgetVisibility();
                break;

            case nameof(MarkManyViewModel<ISnesData>.RegisterValueMaxTextLength):
                WriteWidgets(() => RegValueBox.MaxLength = vm.RegisterValueMaxTextLength);
                break;

            case nameof(MarkManyViewModel<ISnesData>.DataBankValue):
            case nameof(MarkManyViewModel<ISnesData>.DirectPageValue):
                RefreshRegisterValueText();
                break;

            case nameof(MarkManyViewModel<ISnesData>.FlagValue):
                WriteWidgets(() =>
                    FlagCombo.SelectedIndex = Array.IndexOf(FlagComboValues, vm.FlagValue));
                break;

            case nameof(MarkManyViewModel<ISnesData>.RegisterWidthIs8Bit):
                WriteWidgets(() =>
                    MxCombo.SelectedIndex = vm.RegisterWidthIs8Bit ? MxComboIndex8Bit : MxComboIndex16Bit);
                break;

            case nameof(MarkManyViewModel<ISnesData>.ArchitectureValue):
                WriteWidgets(() =>
                    ArchCombo.SelectedIndex = Array.IndexOf(ArchComboValues, vm.ArchitectureValue));
                break;
        }

        RefreshValidation();
    }

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
                // the register value box shares the range's number base
                WriteWidgets(() =>
                {
                    RadioHex.IsChecked = vm.Range.UseHexadecimal;
                    RadioDec.IsChecked = !vm.Range.UseHexadecimal;
                });
                RefreshRegisterValueText();
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
            PropertyCombo.SelectedIndex = Array.IndexOf(PropertyComboValues, vm.SelectedProperty);
            FlagCombo.SelectedIndex = Array.IndexOf(FlagComboValues, vm.FlagValue);
            ArchCombo.SelectedIndex = Array.IndexOf(ArchComboValues, vm.ArchitectureValue);
            MxCombo.SelectedIndex = vm.RegisterWidthIs8Bit ? MxComboIndex8Bit : MxComboIndex16Bit;

            RadioSnes.IsChecked = vm.Range.UseSnesAddresses;
            RadioPc.IsChecked = !vm.Range.UseSnesAddresses;
            RadioHex.IsChecked = vm.Range.UseHexadecimal;
            RadioDec.IsChecked = !vm.Range.UseHexadecimal;

            StartBox.Text = vm.Range.StartText;
            EndBox.Text = vm.Range.EndText;
            CountBox.Text = vm.Range.CountText;

            RegValueBox.MaxLength = vm.RegisterValueMaxTextLength;
            RegValueBox.Text = RegisterValueText();
        });

        RefreshValueWidgetVisibility();
        RefreshValidation();
    }

    private void RefreshValueWidgetVisibility() =>
        WriteWidgets(() =>
        {
            FlagCombo.IsVisible = vm?.IsFlagValueUsed ?? false;
            RegValueBox.IsVisible = vm?.IsRegisterValueUsed ?? false;
            MxCombo.IsVisible = vm?.IsRegisterWidthUsed ?? false;
            ArchCombo.IsVisible = vm?.IsArchitectureValueUsed ?? false;
        });

    private void RefreshRegisterValueText() => WriteText(RegValueBox, RegisterValueText());

    private string RegisterValueText() =>
        vm == null
            ? ""
            : Util.NumberToBaseString(
                vm.SelectedProperty == MarkCommand.MarkManyProperty.DirectPage
                    ? vm.DirectPageValue
                    : vm.DataBankValue,
                NumberBase,
                0);

    /// <summary>
    /// OK is available only when the ViewModel can actually build a command; when it can't, the
    /// ViewModel's own message is shown beneath the fields. The view never re-derives whether
    /// the state is valid, it only displays the answer.
    /// </summary>
    private void RefreshValidation()
    {
        var result = vm?.Validate() ?? ValidationResult.Fail("");
        OkButton.IsEnabled = result.IsValid;
        ErrorText.Text = result.IsValid ? "" : result.Error;
    }

    // ------------------------------------------------------------------ widgets -> ViewModel

    private void PropertyCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        PushToViewModel(null, () =>
        {
            if (vm != null && PropertyCombo.SelectedIndex >= 0)
                vm.SelectedProperty = PropertyComboValues[PropertyCombo.SelectedIndex];
        });

    private void FlagCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        PushToViewModel(null, () =>
        {
            if (vm != null && FlagCombo.SelectedIndex >= 0)
                vm.FlagValue = FlagComboValues[FlagCombo.SelectedIndex];
        });

    private void MxCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        PushToViewModel(null, () =>
        {
            if (vm != null && MxCombo.SelectedIndex >= 0)
                vm.RegisterWidthIs8Bit = MxCombo.SelectedIndex == MxComboIndex8Bit;
        });

    private void ArchCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        PushToViewModel(null, () =>
        {
            if (vm != null && ArchCombo.SelectedIndex >= 0)
                vm.ArchitectureValue = ArchComboValues[ArchCombo.SelectedIndex];
        });

    private void StartBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        PushText(StartBox, vm?.Range.StartText, text => vm!.Range.StartText = text);

    private void EndBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        PushText(EndBox, vm?.Range.EndText, text => vm!.Range.EndText = text);

    private void CountBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        PushText(CountBox, vm?.Range.CountText, text => vm!.Range.CountText = text);

    private void RegValueBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        PushText(RegValueBox, RegisterValueText(), text =>
        {
            // unparseable text is ignored outright, exactly as this box has always behaved:
            // the last good number stays in effect until something parseable is typed.
            if (!int.TryParse(text, NumberStyle, CultureInfo.InvariantCulture, out var value))
                return;

            switch (vm!.SelectedProperty)
            {
                case MarkCommand.MarkManyProperty.DataBank:
                    vm.DataBankValue = value;
                    break;
                case MarkCommand.MarkManyProperty.DirectPage:
                    vm.DirectPageValue = value;
                    break;
            }
        });

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

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        // guard: OK is disabled while invalid, but IsDefault means Enter can reach it too.
        if (vm?.CanBuildMarkCommand != true)
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

    private Util.NumberBase NumberBase =>
        vm?.Range.UseHexadecimal ?? true ? Util.NumberBase.Hexadecimal : Util.NumberBase.Decimal;

    private NumberStyles NumberStyle =>
        vm?.Range.UseHexadecimal ?? true ? NumberStyles.HexNumber : NumberStyles.Number;

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

    private void WriteText(TextBox textBox, string text)
    {
        if (ReferenceEquals(textBox, controlBeingEdited))
            return;

        WriteWidgets(() => textBox.Text = text);
    }
}
