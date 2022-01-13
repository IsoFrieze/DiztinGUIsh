using System;
using System.Collections.Generic;
using Diz.Controllers.interfaces;
using Diz.Core;
using Diz.Core.commands;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Controllers.controllers;

public class MarkManyController<TDataSource> : IMarkManyController<TDataSource> where TDataSource 
    : IRomSize
{
    public IDataRange DataRange { get; }
    public TDataSource Data { get; }
    public IMarkManyView<TDataSource> MarkManyView { get; }
    public int DesiredStartingCount { get; set; } = 0x10;

    public MarkManyController(int offset, MarkCommand.MarkManyProperty initialProperty, TDataSource data, IMarkManyView<TDataSource> view)
    {
        Data = data;
        MarkManyView = view;
        MarkManyView.Controller = this;

        DataRange = new CorrectingRange(Data.GetRomSize());

        DataRange.StartIndex = offset;
        DataRange.RangeCount = Math.Min(
            DesiredStartingCount, 
            DataRange.MaxCount - DataRange.StartIndex
        );

        MarkManyView.Property = initialProperty;
    }

    private MarkCommand CreateCommandFromView() =>
        new()
        {
            Start = DataRange.StartIndex,
            Count = DataRange.RangeCount,
            Value = MarkManyView.GetPropertyValue(),
            Property = MarkManyView.Property,
        };

    public MarkCommand GetMarkCommand()
    {
        // attempt to set to previous values from last run, if they are compatible
        MarkManyView.AttemptSetSettings(Settings);
        var command = !MarkManyView.PromptDialog() ? null : CreateCommandFromView();
        Settings = MarkManyView.SaveCurrentSettings();
        return command;
    }

    public Dictionary<MarkCommand.MarkManyProperty, object> Settings { get; set; } = new();
}