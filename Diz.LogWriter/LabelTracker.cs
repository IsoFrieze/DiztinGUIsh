﻿using System.Collections.Generic;
using Diz.Core.export;
using Diz.Core.model;

namespace Diz.LogWriter
{
    public class LabelTracker
    {
        public LabelTracker(ILogCreatorForGenerator logCreator)
        {
            this.logCreator = logCreator;
        }

        private readonly ILogCreatorForGenerator logCreator;
        public ILogCreatorDataSource Data => logCreator?.Data;
        public List<int> VisitedLabelSnesAddresses { get; } = new();

        public Dictionary<int, IReadOnlyLabel> UnvisitedLabels
        {
            get
            {
                CacheUnvisitedLabels();
                return cachedUnvisitedLabels;
            }
        }

        private Dictionary<int, IReadOnlyLabel> cachedUnvisitedLabels;

        public void OnLabelVisited(int snesAddress)
        {
            VisitedLabelSnesAddresses.Add(snesAddress);
            cachedUnvisitedLabels = null;
        }

        private void CacheUnvisitedLabels()
        {
            cachedUnvisitedLabels = new();
            if (VisitedLabelSnesAddresses == null)
                return;

            foreach (var (snesAddress, label) in Data.Labels.Labels)
            {
                if (VisitedLabelSnesAddresses.Contains(snesAddress))
                    continue;

                cachedUnvisitedLabels.Add(snesAddress, label);
            }
        }
    }
}