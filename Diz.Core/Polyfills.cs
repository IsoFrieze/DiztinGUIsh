#nullable enable

namespace Diz.Core
{
    internal static class CollectionExtensions
    {
#if !(NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER)
        public static bool TryAdd<TKey, TValue>(
            this System.Collections.Generic.IDictionary<TKey, TValue> dictionary,
            TKey key,
            TValue value)
        {
            if (dictionary is null) throw new System.ArgumentNullException(paramName: nameof(dictionary));
            if (dictionary.ContainsKey(key)) return false;
            dictionary.Add(key, value);
            return true;
        }
#endif
    }
}