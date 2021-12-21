using System.Collections.Generic;
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
            if (cachedUnvisitedLabels != null) // pretty sure this is the right thing to do?
                return;
            
            cachedUnvisitedLabels = new Dictionary<int, IReadOnlyLabel>();
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