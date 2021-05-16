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
        public IViewer View => MarkManyView;
        public IMarkManyView MarkManyView { get; set; }
        
        public IReadOnlySnesRom Data { get; }
        public MarkManyController(int offset, int column, IReadOnlySnesRom data)
        {
            Data = data;
            const int desiredStartingCount = 0x10;
            SetRangeDefaults(offset, desiredStartingCount);

            // this view can allow edits for different column types on the grid.
            // set which one we want here. (TODO: this is all index-based and should be made
            // more flexible and less hardcoded)
            MarkManyView.Column = column;
        }

        private void SetRangeDefaults(int startingRomOffset, int desiredStartingCount)
        {
            DataRange.MaxCount = Data.GetRomSize();
            DataRange.StartIndex = startingRomOffset;
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