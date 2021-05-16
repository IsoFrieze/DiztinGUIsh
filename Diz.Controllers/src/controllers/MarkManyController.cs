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
        public IDataRange DataRange { get; } = new CorrectingRange();
        public IMarkManyView MarkManyView { get; }
        public IReadOnlySnesRom Data { get; }
        public int DesiredStartingCount { get; set; } = 0x10;

        public MarkManyController(int offset, int whichIndex, IReadOnlySnesRom data, IMarkManyView view)
        {
            Data = data;
            MarkManyView = view;
            MarkManyView.Controller = this;
            
            SetOffset(offset);
            
            MarkManyView.Column = whichIndex;
        }

        private void SetOffset(int offset)
        {
            var desiredStartingCount = DesiredStartingCount;
            if (desiredStartingCount == -1)
                desiredStartingCount = DesiredStartingCount;
            
            DataRange.MaxCount = Data.GetRomSize();
            DataRange.StartIndex = offset;
            var actualAmountAvailable = DataRange.MaxCount - DataRange.StartIndex;
            DataRange.RangeCount = Math.Min(desiredStartingCount, actualAmountAvailable);
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