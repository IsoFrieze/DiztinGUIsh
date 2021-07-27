using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Diz.Core.util;
using DynamicData;

namespace Diz.Gui.ViewModels.ViewModels
{
    public static class FakeLabelService
    {
        private static Lazy<SourceCache<LabelViewModel, int>> SourceLabels
            = new(valueFactory: () =>
            {
                var sourceCache = new SourceCache<LabelViewModel, int>(x => x.Offset);
                sourceCache.AddOrUpdate(CreateSampleLabels());
                return sourceCache;
            });

        public static IObservable<IChangeSet<LabelViewModel, int>> Connect() =>
            SourceLabels.Value.Connect();

        private static IEnumerable<LabelViewModel> CreateSampleLabels()
        {
            var labels = SampleRomData.CreateSampleData().Labels;
            var labelsValues = labels.Select(
                kvp => new LabelViewModel {Label = kvp.Value, Offset = kvp.Key});
            
            return labelsValues;
        }
    }
}