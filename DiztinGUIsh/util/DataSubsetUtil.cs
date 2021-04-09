using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using DiztinGUIsh.window2;

namespace DiztinGUIsh.util
{
    public class DataSubsetLookaheadCacheRomByteDataGridLoader<TRow, TItem> : 
        DataSubsetLookaheadCacheLoader<TRow, TItem>
        where TItem : ByteOffsetData
        where TRow : class, IGridRow<TItem>
    {
        public IBytesGridViewer<TItem> View { get; init; }
        public Data Data { get; init; }

        protected override TRow CreateNewRow(DataSubset<TRow, TItem> subset, int largeIndex)
        {
            return new RomByteDataGridRow
            {
                ByteOffset = subset.Items[largeIndex],
                Data = Data,
                ParentView = View as IBytesGridViewer<ByteOffsetData>,
            } as TRow;
        }
    }
}