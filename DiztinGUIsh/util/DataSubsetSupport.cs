using System;
using System.Collections.Generic;
using System.Linq;
using Diz.Core.model;
using DiztinGUIsh.window2;

namespace DiztinGUIsh.util
{
    public abstract class DataSubsetLookaheadCacheLoaderBase
    {
        public abstract RomByteDataGridRow GetOrCreateRow(int largeOffset, DataSubset subset);
        public abstract void OnBigWindowChangeFinished(DataSubset subset);
        public abstract void OnBigWindowChangeStart(DataSubset subset);
    }

    public class DataSubsetLookaheadCacheLoader : DataSubsetLookaheadCacheLoaderBase
    {
        public class Entry
        {
            public RomByteDataGridRow row;
            public int ageScore; // 0 = newer, higher = older
            public int cachedLargeIndex;

            public int GetDistanceScore(DataSubset dataSubset)
            {
                return GetDistanceScore(dataSubset.StartingRowLargeIndex, dataSubset.RowCount);
            }

            public int GetDistanceScore(int viewLargeStartIndex, int viewCount)
            {
                var distanceFromStart = Math.Abs(cachedLargeIndex - viewLargeStartIndex);
                var distanceFromEnd = Math.Abs(cachedLargeIndex - viewLargeStartIndex + viewCount);

                return Math.Min(distanceFromStart, distanceFromEnd);
            }

            public bool IsOlderThan(Entry value) => ageScore > value.ageScore;

            public bool IsDistanceGreaterThan(DataSubset dataSubset, int otherDistanceScore)
            {
                return IsDistanceGreaterThan(dataSubset.StartingRowLargeIndex, dataSubset.RowCount, otherDistanceScore);
            }

            public bool IsDistanceGreaterThan(int viewLargeStartIndex, int viewCount, int otherDistanceScore)
            {
                var myDistanceScore =
                    GetDistanceScore(viewLargeStartIndex, viewCount);

                return myDistanceScore > otherDistanceScore;
            }
        }

        // map large data index offset to a row
        // 
        // sometimes this will contain extra or not enough rows, and we'll page them in and out as needed.
        // this dictionary ALWAYS includes all of the currently displayed rows,
        // but also, can include more cached rows that we can kick out as needed to save memory.
        private readonly Dictionary<int, Entry> cachedRows = new();

        public IBytesGridViewer<RomByteData> View { get; init; }

        // tune as needed. if user can see about 20 rows at a time, we'll keep around 10x that in memory.
        // if cached rows are in memory, it'll make small scrolling (like bouncing around near the same
        // couple of rows) already cached
        public int TargetCachedMultiplier { get; init; } = 10;
        
        public int TargetCachedRows
        {
            get => targetCachedRows;
            protected set => targetCachedRows = value;
        }

        // we'll allow going a certain percentage over the target before cleaning up.
        // that way we're only cleaning up in chunks and not in individual rows.
        public int FuzzThreshold => (int) (TargetCachedRows * 0.1f);

        public override void OnBigWindowChangeStart(DataSubset subset)
        {
            TargetCachedRows = subset.RowCount * TargetCachedMultiplier;
            
            IncrementAllAgeScores();
        }

        private void IncrementAllAgeScores()
        {
            foreach (var entry in cachedRows)
                entry.Value.ageScore++;
        }

        public override RomByteDataGridRow GetOrCreateRow(int largeOffset, DataSubset subset)
        {
            var entry = GetOrCreateRowEntry(largeOffset, subset);
            entry.ageScore = 0; // any new rows always aged at zero
            return entry.row;
        }

        private Entry GetOrCreateRowEntry(int largeIndex, DataSubset subset)
        {
            if (cachedRows.TryGetValue(largeIndex, out var entry))
                return entry;

            var dataRomByte = subset.RomBytes[largeIndex];

            // assume this creation is expensive, we're optimizing to minimize # initializations here
            entry = new Entry()
            {
                row = new RomByteDataGridRow(dataRomByte, subset.Data, View),
                cachedLargeIndex = largeIndex,
            };

            cachedRows[largeIndex] = entry;
            return entry;
        }

        private readonly List<Entry> tmpEntriesForDeletion = new();
        private int targetCachedRows;

        // this is a hint that big changes just finished up (like recreating the rows due to a scroll),
        // so it's likely a good time to kick irrelevant rows out of the cache.
        //
        // we could do a bunch of clever stuff, I'm just going to a really simple distance check
        // and kick out the oldest rows (rows that haven't been in any view for a while)
        // which are furthest away from the current window
        public override void OnBigWindowChangeFinished(DataSubset subset)
        {
            tmpEntriesForDeletion.Clear();

            // see if we're about 10% over our target, and if so, dump about 10% of the cache.
            // it's OK to go over so that we're not constantly dumping cache with every small change.
            if (cachedRows.Count <= TargetCachedRows + FuzzThreshold)
                return;

            MarkOldestAndFarthestItemsForDeletion();
            DeleteAnyMarkedItems();
            
            void MarkOldestAndFarthestItemsForDeletion()
            {
                var targetDeletionCount = FuzzThreshold;

                // time to clear out rows til we're under target
                var oldestDeletionCandidates = from kvp in cachedRows
                    where kvp.Value.ageScore != 0
                    orderby kvp.Value.ageScore
                    select kvp;

                foreach (var (_, value) in oldestDeletionCandidates)
                {
                    var myDistanceScore =
                        value.GetDistanceScore(subset);
                    
                    if (tmpEntriesForDeletion.Count <= targetDeletionCount)
                    {
                        tmpEntriesForDeletion.Add(value);
                        continue;
                    }
                    
                    for (var iDelete = 0; iDelete < tmpEntriesForDeletion.Count; iDelete++)
                    {
                        var theyAreOlder = tmpEntriesForDeletion[iDelete].IsOlderThan(value);
                        var theyAreFurther = tmpEntriesForDeletion[iDelete].IsDistanceGreaterThan(subset, myDistanceScore);

                        // could do something fancier here, but, this is probably fine
                        var weAreBetter = theyAreFurther || theyAreOlder; 
                        if (weAreBetter)
                            continue;

                        tmpEntriesForDeletion[iDelete] = value;
                    }
                }
            }
            
            void DeleteAnyMarkedItems()
            {
                foreach (var itemToDelete in tmpEntriesForDeletion) 
                    cachedRows.Remove(itemToDelete.cachedLargeIndex);

                tmpEntriesForDeletion.Clear();
            }
        }
    }
}