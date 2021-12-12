using System;
using System.Collections.Generic;
using Diz.Core;
using Diz.Core.commands;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.controller;

namespace Diz.Controllers.controllers
{
    public class MarkManyController : IMarkManyController
    {
        public IDataRange DataRange { get; }
        public IMarkManyView MarkManyView { get; }
        public IReadOnlySnesRomBase Data { get; }
        public int DesiredStartingCount { get; set; } = 0x10;

        public MarkManyController(int offset, MarkCommand.MarkManyProperty initialProperty, IReadOnlySnesRomBase data, IMarkManyView view)
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
}