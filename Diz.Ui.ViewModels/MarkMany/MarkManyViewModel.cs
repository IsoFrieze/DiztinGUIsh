using Diz.Core.commands;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Ui.ViewModels.MarkMany;

/// <summary>
/// "Mark many": pick one CPU/annotation property and one value, pick a run of bytes, and
/// produce a <see cref="MarkCommand"/> describing that edit.
///
/// This ViewModel BUILDS the command and never applies it. Applying is
/// SnesApiExtensions.ApplyMarkCommand in Diz.Cpu.65816 -- an assembly this one is not allowed
/// to reference, and the separation is the point: the same command can come from a window, a
/// script, or an external API, and one applier handles all of them.
///
/// One value is remembered per property (not one shared value), so switching the property
/// selector back and forth doesn't destroy what you typed for the other one. The exception is
/// the M/X pair, which shares a single 8-bit/16-bit choice -- only one of them is ever
/// selectable at a time and the register width means the same thing to both.
///
/// Data bank / direct page reseed from the ROM whenever the property selection changes: the
/// useful default when you say "mark the data bank here" is the data bank that is already
/// recorded at the start of the range. Restoring a saved snapshot is the one exception -- see
/// <see cref="RestoreSettings"/>.
/// </summary>
/// <typeparam name="TDataSource">
/// The ROM being edited, read-only from this ViewModel's point of view: its size, its
/// address conversion, and the CPU state already recorded at an offset.
/// </typeparam>
public sealed class MarkManyViewModel<TDataSource> : ViewModelNotifierBase
    where TDataSource : IRomSize, IRomByteFlagsGettable, ISnesAddressConverter
{
    /// <summary>Data bank is one byte: 0..$FF.</summary>
    public const int MaxDataBankValue = 0xFF;

    /// <summary>Direct page is a 16-bit register: 0..$FFFF.</summary>
    public const int MaxDirectPageValue = 0xFFFF;

    private readonly TDataSource data;

    private MarkCommand.MarkManyProperty selectedProperty = MarkCommand.MarkManyProperty.Flag;
    private FlagType flagValue = FlagType.Data8Bit;
    private int dataBankValue;
    private int directPageValue;
    private bool registerWidthIs8Bit;
    private Architecture architectureValue = Architecture.Cpu65C816;

    /// <param name="data">The ROM being marked.</param>
    /// <param name="startOffset">ROM file offset the range starts at (clamped to the ROM).</param>
    /// <param name="count">How many bytes to mark (clamped so the range stays inside the ROM).</param>
    /// <param name="notificationMarshaller">See <see cref="ViewModelNotifierBase"/>.</param>
    public MarkManyViewModel(
        TDataSource data,
        int startOffset = 0,
        int count = 0x10,
        Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        ArgumentNullException.ThrowIfNull(data);

        this.data = data;
        Range = new AddressRangeViewModel(data, data.GetRomSize(), NotificationMarshaller);
        Range.SetRange(startOffset, count);

        // seed both register values from whatever the ROM already records at the start of
        // the range, so the initial selection shows a meaningful default either way.
        dataBankValue = data.GetDataBank(Range.StartIndex);
        directPageValue = data.GetDirectPage(Range.StartIndex);
    }

    /// <summary>The run of bytes to mark.</summary>
    public AddressRangeViewModel Range { get; }

    /// <summary>
    /// Which property gets marked. Assigning it -- even assigning the value it already has --
    /// reseeds the matching register value from the ROM at the start of the range.
    /// </summary>
    public MarkCommand.MarkManyProperty SelectedProperty
    {
        get => selectedProperty;
        set
        {
            var changed = selectedProperty != value;
            selectedProperty = value;

            ReseedRegisterValueFromRom();

            if (!changed)
                return;

            OnPropertyChanged(nameof(SelectedProperty));
            OnPropertyChanged(nameof(IsFlagValueUsed));
            OnPropertyChanged(nameof(IsRegisterValueUsed));
            OnPropertyChanged(nameof(IsRegisterWidthUsed));
            OnPropertyChanged(nameof(IsArchitectureValueUsed));
            OnPropertyChanged(nameof(MaxRegisterValue));
            OnPropertyChanged(nameof(RegisterValueMaxTextLength));
            OnPropertyChanged(nameof(SelectedValue));
        }
    }

    // ------------------------------------------------------------------
    // per-property values
    // ------------------------------------------------------------------

    /// <summary>Value used when <see cref="SelectedProperty"/> is Flag.</summary>
    public FlagType FlagValue
    {
        get => flagValue;
        set
        {
            if (!this.SetField(ref flagValue, value))
                return;
            OnPropertyChanged(nameof(SelectedValue));
        }
    }

    /// <summary>
    /// Value used when <see cref="SelectedProperty"/> is DataBank. Stored exactly as given --
    /// out-of-range input is reported by <see cref="Validate"/>, not silently corrected, so
    /// nothing is marked with a number the user never intended.
    /// </summary>
    public int DataBankValue
    {
        get => dataBankValue;
        set
        {
            if (!this.SetField(ref dataBankValue, value))
                return;
            OnPropertyChanged(nameof(SelectedValue));
        }
    }

    /// <summary>Value used when <see cref="SelectedProperty"/> is DirectPage. See <see cref="DataBankValue"/>.</summary>
    public int DirectPageValue
    {
        get => directPageValue;
        set
        {
            if (!this.SetField(ref directPageValue, value))
                return;
            OnPropertyChanged(nameof(SelectedValue));
        }
    }

    /// <summary>
    /// Value used when <see cref="SelectedProperty"/> is MFlag or XFlag: true marks the
    /// register as 8-bit, false as 16-bit. Shared by both flags.
    /// </summary>
    public bool RegisterWidthIs8Bit
    {
        get => registerWidthIs8Bit;
        set
        {
            if (!this.SetField(ref registerWidthIs8Bit, value))
                return;
            OnPropertyChanged(nameof(SelectedValue));
        }
    }

    /// <summary>Value used when <see cref="SelectedProperty"/> is CpuArch.</summary>
    public Architecture ArchitectureValue
    {
        get => architectureValue;
        set
        {
            if (!this.SetField(ref architectureValue, value))
                return;
            OnPropertyChanged(nameof(SelectedValue));
        }
    }

    // ------------------------------------------------------------------
    // which value input is in play for the current selection
    // ------------------------------------------------------------------

    public bool IsFlagValueUsed => selectedProperty == MarkCommand.MarkManyProperty.Flag;

    public bool IsRegisterValueUsed =>
        selectedProperty is MarkCommand.MarkManyProperty.DataBank or MarkCommand.MarkManyProperty.DirectPage;

    public bool IsRegisterWidthUsed =>
        selectedProperty is MarkCommand.MarkManyProperty.MFlag or MarkCommand.MarkManyProperty.XFlag;

    /// <summary>
    /// True when the CPU-architecture input is the one in play. Marking CPU architecture works
    /// end to end here and in the applier, but no window lists it in its property selector
    /// today -- add it there if it ever becomes relevant.
    /// </summary>
    public bool IsArchitectureValueUsed => selectedProperty == MarkCommand.MarkManyProperty.CpuArch;

    /// <summary>Largest accepted value for the register input, given the current selection.</summary>
    public int MaxRegisterValue =>
        selectedProperty == MarkCommand.MarkManyProperty.DataBank ? MaxDataBankValue : MaxDirectPageValue;

    /// <summary>
    /// How many characters the register input can hold: 3 for a data bank, 5 for a direct
    /// page. Enough for the decimal form of each maximum.
    /// </summary>
    public int RegisterValueMaxTextLength =>
        selectedProperty == MarkCommand.MarkManyProperty.DataBank ? 3 : 5;

    // ------------------------------------------------------------------
    // the command
    // ------------------------------------------------------------------

    /// <summary>
    /// The value that would be written into <see cref="MarkCommand.Value"/> right now. Boxed,
    /// because MarkCommand.Value is object and its runtime type varies by property.
    /// </summary>
    public object SelectedValue => ValueFor(selectedProperty);

    /// <summary>Why the current state can't be turned into a command, or IsValid.</summary>
    public ValidationResult Validate()
    {
        if (!Enum.IsDefined(selectedProperty))
            return ValidationResult.Fail("Pick a property to mark.");

        if (Range.Count <= 0)
            return ValidationResult.Fail("The range is empty: pick at least one byte to mark.");

        switch (selectedProperty)
        {
            case MarkCommand.MarkManyProperty.Flag when !Enum.IsDefined(flagValue):
                return ValidationResult.Fail("Pick a valid flag type.");

            case MarkCommand.MarkManyProperty.DataBank when dataBankValue is < 0 or > MaxDataBankValue:
                return ValidationResult.Fail($"Data bank must be between 0 and ${MaxDataBankValue:X}.");

            case MarkCommand.MarkManyProperty.DirectPage when directPageValue is < 0 or > MaxDirectPageValue:
                return ValidationResult.Fail($"Direct page must be between 0 and ${MaxDirectPageValue:X}.");

            case MarkCommand.MarkManyProperty.CpuArch when !Enum.IsDefined(architectureValue):
                return ValidationResult.Fail("Pick a valid CPU architecture.");
        }

        return ValidationResult.Ok;
    }

    /// <summary>True when <see cref="BuildMarkCommand"/> will return a command.</summary>
    public bool CanBuildMarkCommand => Validate().IsValid;

    /// <summary>
    /// The command describing what the user asked for, or null if the current state isn't
    /// valid (see <see cref="Validate"/>). Applying it is the caller's job.
    /// </summary>
    public MarkCommand? BuildMarkCommand() =>
        !Validate().IsValid
            ? null
            : new MarkCommand
            {
                Property = selectedProperty,
                Start = Range.StartIndex,
                Count = Range.Count,
                Value = ValueFor(selectedProperty),
            };

    // ------------------------------------------------------------------
    // session memory
    // ------------------------------------------------------------------

    /// <summary>Snapshot every property's value plus the current selection.</summary>
    public MarkManySettings CaptureSettings()
    {
        var settings = new MarkManySettings { SelectedProperty = selectedProperty };

        foreach (var property in Enum.GetValues<MarkCommand.MarkManyProperty>())
            settings.AllSettings[property] = ValueFor(property);

        return settings;
    }

    /// <summary>
    /// Restore a snapshot. Entries whose stored value doesn't fit the property are ignored,
    /// so a stale or hand-built snapshot degrades to defaults instead of throwing.
    ///
    /// A restored value always wins: the selection is applied FIRST and the remembered values
    /// second, so the data bank / direct page reseed that the selection triggers cannot
    /// overwrite what was remembered. Only a property the snapshot has no usable entry for
    /// keeps the reseeded value. Switching the selection afterwards still reseeds as usual --
    /// that is the interactive behavior and it is unaffected by this method.
    /// </summary>
    public void RestoreSettings(MarkManySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        SelectedProperty = settings.SelectedProperty;

        foreach (var (property, value) in settings.AllSettings)
            RestoreValue(property, value);
    }

    private void RestoreValue(MarkCommand.MarkManyProperty property, object value)
    {
        switch (property)
        {
            case MarkCommand.MarkManyProperty.Flag when value is FlagType flag:
                FlagValue = flag;
                break;
            case MarkCommand.MarkManyProperty.DataBank when value is int dataBank:
                DataBankValue = dataBank;
                break;
            case MarkCommand.MarkManyProperty.DirectPage when value is int directPage:
                DirectPageValue = directPage;
                break;
            case MarkCommand.MarkManyProperty.MFlag or MarkCommand.MarkManyProperty.XFlag
                when value is bool is8Bit:
                RegisterWidthIs8Bit = is8Bit;
                break;
            case MarkCommand.MarkManyProperty.CpuArch when value is Architecture architecture:
                ArchitectureValue = architecture;
                break;
        }
    }

    // ------------------------------------------------------------------
    // internals
    // ------------------------------------------------------------------

    private object ValueFor(MarkCommand.MarkManyProperty property) =>
        property switch
        {
            MarkCommand.MarkManyProperty.Flag => flagValue,
            MarkCommand.MarkManyProperty.DataBank => dataBankValue,
            MarkCommand.MarkManyProperty.DirectPage => directPageValue,
            MarkCommand.MarkManyProperty.MFlag => registerWidthIs8Bit,
            MarkCommand.MarkManyProperty.XFlag => registerWidthIs8Bit,
            MarkCommand.MarkManyProperty.CpuArch => architectureValue,
            _ => 0,
        };

    private void ReseedRegisterValueFromRom()
    {
        switch (selectedProperty)
        {
            case MarkCommand.MarkManyProperty.DataBank:
                DataBankValue = data.GetDataBank(Range.StartIndex);
                break;
            case MarkCommand.MarkManyProperty.DirectPage:
                DirectPageValue = data.GetDirectPage(Range.StartIndex);
                break;
        }
    }
}
