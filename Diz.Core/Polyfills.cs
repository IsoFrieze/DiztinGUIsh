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

    internal static class PathPolyfill
    {
        public static bool IsPathFullyQualified(string path)
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            => System.IO.Path.IsPathFullyQualified(path);
#else // copied from https://github.com/TASEmulators/BizHawk/commit/d9069ea2cc6d36fef16cd533389fd66482aae471
        {
            if (/*OSTailoredCode.IsUnixHost*/!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return /*path.StartsWith(Path.DirectorySeparatorChar)*/path is [ '/', .. ];
            }
            /* Windows:
            var root = System.IO.Path.GetPathRoot(path);
            return root.StartsWith($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}")
                || (root.Length >= 2 && root.EndsWith(Path.DirectorySeparatorChar));
            */
            return System.IO.Path.GetPathRoot(path) is [ '\\', '\\', .. ] or [ .., _, '\\' ];
        }
#endif
    }
}