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
        //
        // this can be jacked WAY up with little effect except using more memory.
        // hike it if you need more perf.
        public int TargetCachedMultiplier { get; init; } = 15;
        
        public int TargetCachedRows { get; protected set; }

        // we'll allow going a certain percentage over the target before cleaning up.
        // that way we're only cleaning up in chunks and not in individual rows.
        public int FuzzThreshold => (int)(TargetCachedRows / (float)TargetCachedMultiplier);

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
            entry.ageScore = 0; // any recent rows will always be aged at zero
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
            };

            cachedRows[largeIndex] = entry;
            return entry;
        }

        private readonly List<Entry> tmpEntriesForDeletion = new();

        // this is a hint that big changes just finished up (like recreating the rows due to a scroll),
        // so it's likely a good time to kick irrelevant rows out of the cache.
        //
        // we could do a bunch of clever stuff, I'm just going to a really simple age check
        // and kick out the oldest rows (rows that haven't been in any view for a while)
        // which are furthest away from the current window
        public override void OnBigWindowChangeFinished(DataSubset subset)
        {
            tmpEntriesForDeletion.Clear();

            // see if we're about 10% over our target, and if so, dump about 10% of the cache.
            // it's OK to go over so that we're not constantly dumping cache with every small change.
            if (cachedRows.Count <= TargetCachedRows + FuzzThreshold)
                return;
            
            // we're over our target, so start dropping the oldest least useful stuff from the cache
            var oldestDeletionCandidates = (from kvp in cachedRows
                where kvp.Value.ageScore != 0
                orderby kvp.Value.ageScore descending
                select kvp).Take(FuzzThreshold).ToList();

            foreach (var entry in oldestDeletionCandidates)
            {
                cachedRows.Remove(entry.Key);
            }
        }
    }
}