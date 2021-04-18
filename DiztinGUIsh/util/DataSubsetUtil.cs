using Diz.Core.datasubset;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using DiztinGUIsh.controller;

namespace DiztinGUIsh.util
{
    public class DataSubsetLookaheadCacheRomByteDataGridLoader<TRow, TItem> : 
        DataSubsetLookaheadCacheLoader<TRow, TItem>
        where TItem : ByteEntry
        where TRow : class, IGridRow<TItem>
    {
        public IBytesGridViewer<TItem> View { get; init; }
        public Data Data { get; init; }

        protected override TRow CreateNewRow(DataSubset<TRow, TItem> subset, int largeIndex)
        {
            return new RomByteDataGridRow
            {
                ByteEntry = subset.Items[largeIndex],
                Data = Data,
                ParentView = View as IBytesGridViewer<ByteEntry>,
            } as TRow;
        }
    }
}