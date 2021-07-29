using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Diz.Core.util;
using DynamicData;

namespace Diz.Gui.ViewModels.ViewModels
{
    public static class LabelSearchEx
    {
        public static IObservable<IEnumerable<LabelViewModel>> SearchForLabels(this IObservable<string> searchQuery)
        {
            return searchQuery
                .Select(Util.StripHex)
                .SelectMany(SearchForLabelsAsync);
        }
        
        
        public static bool Matches(this LabelViewModel @this, string addressSubsetToMatch) =>
            string.IsNullOrEmpty(addressSubsetToMatch) ||
            Util.StripHex(@this.SnesAddress).Contains(addressSubsetToMatch);

        public static async Task<IEnumerable<LabelViewModel>> SearchForLabelsAsync(
            string searchTerm,
            CancellationToken token)
        {
            return await Task.Run(() =>
            {
                return SampleDataService
                    .SourceData.Value
                    .ConnectLabels()
                    .Transform(labelProxy => new LabelViewModel{LabelProxy=labelProxy})
                    .Filter(vm =>
                        vm.Matches(searchTerm)
                    ).AsObservableCache().Items;
            }, token);
        }
    }
}