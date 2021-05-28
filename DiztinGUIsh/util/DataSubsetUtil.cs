using Diz.Controllers;
using Diz.Controllers.interfaces;
using Diz.Core.datasubset;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;

namespace DiztinGUIsh.util
{
    public class DataSubsetRomByteDataGridLoader<TRow, TItem> : 
        DataSubsetLoader<TRow, TItem>, IDataSubsetRomByteDataGridLoader<TRow, TItem>
        where TItem : ByteEntry
        where TRow : class, IGridRow<TItem>
    {
        public IBytesGridViewer<TItem> View { get; set; }
        public Data Data { get; set; }

        protected override TRow CreateNewRow(DataSubset<TRow, TItem> subset, int largeIndex)
        {
            // TODO: replace with dependency injection stuff if we can.
            // get this via asking for an IGridRow<TItem>, and move it back to the controllers project
            // create via IDataSubsetLoader<TRow, TItem>
            return new RomByteDataGridRow
            {
                ByteEntry = subset.Items[largeIndex],
                Data = Data,
                ParentView = View as IBytesGridViewer<ByteEntry>,
            } as TRow;
        }
    }
}