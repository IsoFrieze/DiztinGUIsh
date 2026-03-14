#nullable enable

using Diz.Controllers.interfaces;
using Diz.Core;
using Diz.Core.commands;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Controllers.controllers;

public class MarkManyController<TDataSource> : IMarkManyController<TDataSource> where TDataSource 
    : IRomSize
{
    public TDataSource Data { get; }
    
    public IDataRange DataRange { get; private set; }

    private readonly IMarkManyView<TDataSource> markManyView;

    public MarkManyController(TDataSource data, IMarkManyView<TDataSource> view)
    {
        Data = data;
        markManyView = view;
        markManyView.Controller = this;
        
        DataRange = new CorrectingRange(Data.GetRomSize());
    }

    private MarkCommand BuildCommandFromViewValues() =>
        new()
        {
            Start = DataRange.StartIndex,
            Count = DataRange.RangeCount,
            Value = markManyView.GetPropertyValue(),
            Property = markManyView.Property,
        };

    // returns a command that has parameters selected by the user in the GUI
    // or, null if the user cancels
    public MarkCommand? Show(int startOffset = 0, int count = 0x10, MarkManyViewSettings? inputSettings = null)
    {
        var settingsToUse = inputSettings ?? new MarkManyViewSettings();
        
        DataRange = new CorrectingRange(Data.GetRomSize())
        {
            StartIndex = startOffset,
            RangeCount = count,          // will be clamped if too big for the rom
        };
        
        // attempt to set to previous values from last run, if they are compatible
        markManyView.RestoreUiFromSettings(settingsToUse);
        
        return !markManyView.PromptDialog() 
            ? null 
            : BuildCommandFromViewValues();
    }
    
    public MarkManyViewSettings GetCurrentSettings() => markManyView.BuildSettingsFromUi();
}