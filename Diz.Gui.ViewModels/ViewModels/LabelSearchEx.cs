using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Diz.Core.model;
using Diz.Core.util;
using DynamicData;

namespace Diz.Gui.ViewModels.ViewModels
{
    public static class LabelSearchEx
    {
        public static IObservable<IEnumerable<LabelViewModel>> SearchForLabels(this IObservable<string> searchQuery, IObservable<IChangeSet<LabelProxy, int>> sourceLabels)
        {
            var observable = searchQuery.Select(Util.StripHex);
            
            return observable
                .SelectMany((searchTerm, token) => SearchForLabelsAsync(searchTerm, sourceLabels, token));
        }
        
        public static bool Matches(this LabelViewModel @this, string addressSubsetToMatch) =>
            string.IsNullOrEmpty(addressSubsetToMatch) ||
            Util.StripHex(@this.SnesAddress)
                .Contains(addressSubsetToMatch);

        public static async Task<IEnumerable<LabelViewModel>> SearchForLabelsAsync(string searchTerm,
            IObservable<IChangeSet<LabelProxy, int>> sourceLabels,
            CancellationToken token)
        {
            if (sourceLabels == null)
                return null;
            
            return await Task.Run(() =>
            {
                return sourceLabels
                    .Transform(labelProxy => new LabelViewModel{LabelProxy=labelProxy})
                    .Filter(vm =>
                        vm.Matches(searchTerm)
                    ).AsObservableCache().Items;
            }, token);
        }
    }
}