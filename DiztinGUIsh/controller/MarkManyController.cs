using System;
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

        public MarkManyController(int offset, int whichIndex, IReadOnlySnesRomBase data, IMarkManyView view,
            int lastMarkPropertyIndex = -1)
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
            
            MarkManyView.Column = whichIndex;

            if (lastMarkPropertyIndex != -1)
                MarkManyView.Property = lastMarkPropertyIndex;
        }

        private MarkCommand CreateCommandFromView() =>
            new MarkCommand
            {
                Start = DataRange.StartIndex,
                Count = DataRange.RangeCount,
                Value = MarkManyView.GetFinalValue(),
                PropertyIndex = MarkManyView.Property,
            };

        public MarkCommand GetMarkCommand() => 
            !MarkManyView.PromptDialog() ? null : CreateCommandFromView();
    }
}