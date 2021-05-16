using System;
using Diz.Controllers.interfaces;
using Diz.Core;
using Diz.Core.commands;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Controllers.controllers
{
    public class MarkManyController : IMarkManyController
    {
        public IDataRange DataRange { get; }
        public IMarkManyView MarkManyView { get; }
        public IReadOnlySnesRom Data { get; }
        public int DesiredStartingCount { get; set; } = 0x10;

        public MarkManyController(int offset, int whichIndex, IReadOnlySnesRom data, IMarkManyView view)
        {
            Data = data;
            MarkManyView = view;
            MarkManyView.Controller = this;
            
            DataRange = new CorrectingRange
            {
                MaxCount = Data.GetRomSize()
            };
            
            DataRange.StartIndex = offset;
            DataRange.RangeCount = Math.Min(
                DesiredStartingCount, 
                DataRange.MaxCount - DataRange.StartIndex
            );
            
            MarkManyView.Column = whichIndex;
        }

        public MarkCommand CreateCommandFromView() =>
            new()
            {
                Start = DataRange.StartIndex,
                Count = DataRange.RangeCount,
                Value = MarkManyView.GetFinalValue(),
                Property = MarkManyView.Property,
            };

        public MarkCommand PromptForCommand() => 
            !MarkManyView.PromptDialog() ? null : CreateCommandFromView();
    }
}