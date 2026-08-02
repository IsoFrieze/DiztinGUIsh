using System.ComponentModel;
using System.Globalization;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Ui.ViewModels.Regions;

/// <summary>
/// One region row. Wraps a live region; identity is that region INSTANCE, so sorting, deleting
/// and selecting can never act on the wrong one the way a row index can.
///
/// The row RELAYS the region's own PropertyChanged (regions are INPC), so a change made
/// anywhere -- a detail pane, an import, a migration -- shows up here without a rebind.
///
/// LAST-GOOD vs TYPED TEXT. Every field is exposed as text twice over:
///   LastGoodTextFor(field) is the value the model actually holds;
///   TextFor(field) is what to display, which is the text the user typed when an edit of that
///   field was refused (or ignored), and the committed value otherwise.
/// That split is what lets validation be non-blocking: a bad value can stay on screen, be
/// corrected, and never reach the model in between.
///
/// Lifecycle: the list ViewModel creates rows and MUST Dispose() them when they leave the row
/// set, or the region keeps the row alive via the event subscription.
/// </summary>
public sealed class RegionRowViewModel : ViewModelNotifierBase, IRegionRowViewModel, IDisposable
{
    private readonly IRegion region;
    private readonly Dictionary<RegionField, string> pendingText = new();

    // fields whose typed text was REFUSED, and the message that refused it. Kept separate from
    // pendingText because a blank numeric box is also pending -- it is ignored, not wrong.
    private readonly Dictionary<RegionField, string> refusedFields = new();

    // what the rules say about the values the region ACTUALLY holds right now.
    private string modelErrorText = "";

    private bool hasError;
    private string errorText = "";

    /// <summary>Creation order within the list ViewModel. Used only as the final sort tie-break
    /// so that equal keys never shuffle between re-sorts.</summary>
    internal long Sequence { get; }

    public RegionRowViewModel(IRegion region, long sequence = 0, Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        this.region = region;
        Sequence = sequence;
        region.PropertyChanged += OnModelPropertyChanged;
    }

    public IRegion UnderlyingRegion => region;

    // ---------------------------------------------------------------- TEXT SURFACE

    public string TextFor(RegionField field) =>
        pendingText.TryGetValue(field, out var typed) ? typed : LastGoodTextFor(field);

    public bool HasPendingTextFor(RegionField field) => pendingText.ContainsKey(field);

    public string LastGoodTextFor(RegionField field) => field switch
    {
        RegionField.Start => AddressToText(region.StartSnesAddress),
        RegionField.End => AddressToText(region.EndSnesAddress),
        RegionField.Length => LengthToText(RegionLength),
        RegionField.RegionName => region.RegionName ?? "",
        RegionField.ContextToApply => region.ContextToApply ?? "",
        RegionField.Priority => region.Priority.ToString(CultureInfo.InvariantCulture),
        RegionField.ExportSeparateFile => region.ExportSeparateFile.ToString(),
        RegionField.ExportType => region.ExportType.ToString(),
        RegionField.AssetType => region.AssetType ?? "",
        RegionField.AssetVersion => region.AssetVersion ?? "",
        RegionField.AssetName => region.AssetName ?? "",
        RegionField.AssetOptions => region.AssetOptions ?? "",
        _ => "",
    };

    public string StartText => TextFor(RegionField.Start);
    public string EndText => TextFor(RegionField.End);
    public string LengthText => TextFor(RegionField.Length);
    public string RegionNameText => TextFor(RegionField.RegionName);
    public string ContextToApplyText => TextFor(RegionField.ContextToApply);
    public string PriorityText => TextFor(RegionField.Priority);
    public string ExportSeparateFileText => TextFor(RegionField.ExportSeparateFile);
    public string ExportTypeText => TextFor(RegionField.ExportType);
    public string AssetTypeText => TextFor(RegionField.AssetType);
    public string AssetVersionText => TextFor(RegionField.AssetVersion);
    public string AssetNameText => TextFor(RegionField.AssetName);
    public string AssetOptionsText => TextFor(RegionField.AssetOptions);

    public bool ExportSeparateFile => region.ExportSeparateFile;
    public RegionExportType ExportType => region.ExportType;

    // asset fields are meaningless when the bytes are emitted as plain inline assembly.
    public bool AssetFieldsEnabled => region.ExportType != RegionExportType.Assembly;

    /// <summary>
    /// Byte count of the region. The end address is INCLUSIVE (the last byte IN the region), so
    /// a region whose start equals its end is one byte long, not zero.
    /// </summary>
    public int RegionLength => region.EndSnesAddress - region.StartSnesAddress + 1;

    // ---------------------------------------------------------------- ERROR STATE

    public bool HasError
    {
        get => hasError;
        private set => this.SetField(ref hasError, value);
    }

    public string ErrorText
    {
        get => errorText;
        private set => this.SetField(ref errorText, value ?? "");
    }

    /// <summary>
    /// What the rules say about the values the region ACTUALLY holds; "" when they are legal.
    ///
    /// Deliberately narrower than <see cref="ErrorText"/>, which also speaks for text the user
    /// typed and the model refused. Only the stored half belongs in a whole-list report: an
    /// in-flight refusal is not in the data, and would come and go as the user typed.
    /// </summary>
    internal string ModelErrorText => modelErrorText;

    /// <summary>Record what the rules say about the region's stored values.</summary>
    internal void ApplyValidationResult(ValidationResult result)
    {
        modelErrorText = result.IsValid ? "" : result.Error ?? "";
        RefreshErrorState();
    }

    /// <summary>
    /// A row is flagged while EITHER its stored values break a rule OR it is still displaying
    /// text that was refused. The second half matters: fixing some other field on the row leaves
    /// the stored values legal, but the refused text is still on screen and still not in the
    /// model, so the marker has to stay until that text is dealt with.
    /// </summary>
    private void RefreshErrorState()
    {
        HasError = modelErrorText.Length != 0 || refusedFields.Count != 0;
        ErrorText = modelErrorText.Length != 0
            ? modelErrorText
            : refusedFields.Values.FirstOrDefault() ?? "";
    }

    // ---------------------------------------------------------------- PENDING TEXT

    /// <summary>
    /// Park text the model did not take, so the view can keep showing it.
    /// </summary>
    /// <param name="refusalMessage">why it was refused, or null when the text was merely ignored
    /// (a blank numeric box), which is not a mistake and must not flag the row.</param>
    internal void SetPendingText(RegionField field, string text, string? refusalMessage = null)
    {
        pendingText[field] = text ?? "";

        if (refusalMessage == null)
            refusedFields.Remove(field);
        else
            refusedFields[field] = refusalMessage;

        RefreshErrorState();
        RaiseTextChanged(field);
    }

    internal void ClearPendingText(RegionField field)
    {
        var hadRefusal = refusedFields.Remove(field);
        var hadText = pendingText.Remove(field);

        if (hadRefusal)
            RefreshErrorState();
        if (hadText)
            RaiseTextChanged(field);
    }

    // ---------------------------------------------------------------- MODEL RELAY

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IRegion.StartSnesAddress):
                // the length display is derived from both addresses, so it moved too -- and any
                // length the user had typed is now stale.
                ClearPendingText(RegionField.Start);
                ClearPendingText(RegionField.Length);
                RaiseTextChanged(RegionField.Start);
                RaiseTextChanged(RegionField.Length);
                break;
            case nameof(IRegion.EndSnesAddress):
                ClearPendingText(RegionField.End);
                ClearPendingText(RegionField.Length);
                RaiseTextChanged(RegionField.End);
                RaiseTextChanged(RegionField.Length);
                break;
            case nameof(IRegion.RegionName):
                ClearAndRaise(RegionField.RegionName);
                break;
            case nameof(IRegion.ContextToApply):
                ClearAndRaise(RegionField.ContextToApply);
                break;
            case nameof(IRegion.Priority):
                ClearAndRaise(RegionField.Priority);
                break;
            case nameof(IRegion.ExportSeparateFile):
                ClearAndRaise(RegionField.ExportSeparateFile);
                OnPropertyChanged(nameof(ExportSeparateFile));
                break;
            case nameof(IRegion.ExportType):
                ClearAndRaise(RegionField.ExportType);
                OnPropertyChanged(nameof(ExportType));
                OnPropertyChanged(nameof(AssetFieldsEnabled));
                break;
            case nameof(IRegion.AssetType):
                ClearAndRaise(RegionField.AssetType);
                break;
            case nameof(IRegion.AssetVersion):
                ClearAndRaise(RegionField.AssetVersion);
                break;
            case nameof(IRegion.AssetName):
                ClearAndRaise(RegionField.AssetName);
                break;
            case nameof(IRegion.AssetOptions):
                ClearAndRaise(RegionField.AssetOptions);
                break;
        }
    }

    private void ClearAndRaise(RegionField field)
    {
        // a committed value arrived: whatever the user had typed for this field is history, and
        // so is any refusal of it.
        if (refusedFields.Remove(field))
            RefreshErrorState();

        pendingText.Remove(field);
        RaiseTextChanged(field);
    }

    private void RaiseTextChanged(RegionField field)
    {
        OnPropertyChanged(PropertyNameOf(field));
        if (field is RegionField.Start or RegionField.End)
            OnPropertyChanged(nameof(RegionLength));
    }

    /// <summary>The named text property that carries a field, for INPC purposes.</summary>
    internal static string PropertyNameOf(RegionField field) => field switch
    {
        RegionField.Start => nameof(StartText),
        RegionField.End => nameof(EndText),
        RegionField.Length => nameof(LengthText),
        RegionField.RegionName => nameof(RegionNameText),
        RegionField.ContextToApply => nameof(ContextToApplyText),
        RegionField.Priority => nameof(PriorityText),
        RegionField.ExportSeparateFile => nameof(ExportSeparateFileText),
        RegionField.ExportType => nameof(ExportTypeText),
        RegionField.AssetType => nameof(AssetTypeText),
        RegionField.AssetVersion => nameof(AssetVersionText),
        RegionField.AssetName => nameof(AssetNameText),
        RegionField.AssetOptions => nameof(AssetOptionsText),
        _ => "",
    };

    // ---------------------------------------------------------------- TEXT RENDERING

    // addresses are six hex digits, unprefixed -- the same way they read in the disassembly.
    private static string AddressToText(int address) =>
        Util.NumberToBaseString(address, Util.NumberBase.Hexadecimal, 6, showPrefix: false);

    // the length is hex too, but unpadded: leading zeroes on a byte count are just noise.
    private static string LengthToText(int length) =>
        Util.NumberToBaseString(length, Util.NumberBase.Hexadecimal, 0, showPrefix: false);

    public void Dispose() => region.PropertyChanged -= OnModelPropertyChanged;
}
