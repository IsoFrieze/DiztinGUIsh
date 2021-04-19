using System.Collections.Generic;
using System.Linq;

namespace Diz.Core.datasubset
{
    public abstract class DataSubsetLoader<TRow, TItem> : IDataSubsetLoader<TRow, TItem>
    {
        public class Entry
        {
            public TRow Row { get; set; }
            public int AgeScore { get; set; } // 0 = newer, higher = older
        }

        // map large data index offset to a row
        // 
        // sometimes this will contain extra or not enough rows, and we'll page them in and out as needed.
        // this dictionary ALWAYS includes all of the currently displayed rows,
        // but also, can include more cached rows that we can kick out as needed to save memory.
        private readonly Dictionary<int, Entry> cachedRows = new();

        // tune as needed. if user can see about 20 rows at a time, we'll keep around 10x that in memory.
        // if cached rows are in memory, it'll make small scrolling (like bouncing around near the same
        // couple of rows) already cached
        //
        // this can be jacked WAY up with little effect except using more memory.
        // hike it if you need more perf.
        public int TargetCachedMultiplier { get; init; } = 15;
        
        public int TargetCachedRows { get; protected set; }

        // we'll allow going a certain percentage over the target before cleaning up.
        // that way we're only cleaning up in chunks and not in individual rows.
        public int FuzzThreshold => (int)(TargetCachedRows / (float)TargetCachedMultiplier);

        public virtual void OnBigWindowChangeStart(DataSubset<TRow, TItem> subset)
        {
            TargetCachedRows = subset.RowCount * TargetCachedMultiplier;
            
            IncrementAllAgeScores();
        }

        private void IncrementAllAgeScores()
        {
            foreach (var entry in cachedRows)
                entry.Value.AgeScore++;
        }

        public virtual TRow RowValueNeeded(int largeOffset, DataSubset<TRow, TItem> subset)
        {
            var entry = GetOrCreateRowEntry(largeOffset, subset);
            entry.AgeScore = 0; // any recent rows will always be aged at zero
            return entry.Row;
        }

        private Entry GetOrCreateRowEntry(int largeIndex, DataSubset<TRow, TItem> subset)
        {
            if (cachedRows.TryGetValue(largeIndex, out var entry))
                return entry;

            // assume this creation is expensive, we're optimizing to minimize # initializations here
            entry = new Entry
            {
                Row = CreateNewRow(subset, largeIndex),
            };

            cachedRows[largeIndex] = entry;
            return entry;
        }

        protected abstract TRow CreateNewRow(DataSubset<TRow, TItem> subset, int largeIndex);

        // this is a hint that big changes just finished up (like recreating the rows due to a scroll),
        // so it's likely a good time to kick irrelevant rows out of the cache.
        //
        // we could do a bunch of clever stuff, I'm just going to a really simple age check
        // and kick out the oldest rows (rows that haven't been in any view for a while)
        // which are furthest away from the current window
        public virtual void OnBigWindowChangeFinished(DataSubset<TRow, TItem> subset)
        {
            // see if we're about 10% over our target, and if so, dump about 10% of the cache.
            // it's OK to go over so that we're not constantly dumping cache with every small change.
            if (cachedRows.Count <= TargetCachedRows + FuzzThreshold)
                return;
            
            // we're over our target, so start dropping the oldest least useful stuff from the cache
            var oldestDeletionCandidates = (from kvp in cachedRows
                where kvp.Value.AgeScore != 0
                orderby kvp.Value.AgeScore descending
                select kvp).Take(FuzzThreshold).ToList();

            foreach (var entry in oldestDeletionCandidates)
            {
                cachedRows.Remove(entry.Key);
            }
        }
    }

    public class DataSubsetSimpleLoader<TRow, TItem> : IDataSubsetLoader<TRow, TItem> where TRow : new()
    {
        // no caching, just create a new row as needed each time
        // if performance is an issue, use another strategy.
        public TRow RowValueNeeded(int largeOffset, DataSubset<TRow, TItem> subset)
        {
            TRow newRow = new();

            PopulateRow?.Invoke(ref newRow, largeOffset);

            return newRow;
        }
        
        public delegate void PopulateNewlyCreatedRow(ref TRow newlyCreatedRow, int largeIndex);

        public PopulateNewlyCreatedRow PopulateRow { get; set; }

        public void OnBigWindowChangeStart(DataSubset<TRow, TItem> subset) {}
        public void OnBigWindowChangeFinished(DataSubset<TRow, TItem> subset) {}
    }
}