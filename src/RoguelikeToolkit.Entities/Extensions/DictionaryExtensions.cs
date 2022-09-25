using System.Dynamic;
using System.Runtime.CompilerServices;

namespace RoguelikeToolkit.Entities.Extensions
{
    /// <summary>
    /// A helper class that contains misc dictionary "quality of life" functions.
    /// </summary>
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Get or add to a dictionary, depending on whether the key exists or not.
        /// </summary>
        /// <typeparam name="TKey">Dictionary key type.</typeparam>
        /// <typeparam name="TValue">Dictionary value type.</typeparam>
        /// <param name="dict">The dictionary to operate on.</param>
        /// <param name="key">The key to look for.</param>
        /// <param name="valueFactory">A lambda to generate a value to fill if the key is not there</param>
        /// <returns>Either fetched or newly created value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="Failed to read the key. This is not supposed to happen and is likely a misuse of the system."/> is <see langword="null"/></exception>
        /// <exception cref="Exception">A valueFactory might throw an exception!</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> valueFactory)
            where TKey : notnull
        {
            if (key == null)
            {
                throw new ArgumentNullException(
                    nameof(key), "Failed to read the key. This is not supposed to happen and is likely a misuse of the system.");
            }

            if (dict.TryGetValue(key, out var value))
            {
                return value;
            }

            var newValue = valueFactory(key);
            dict.Add(key, newValue);

            return newValue;
        }

        /// <summary>
        /// Merge two dictionaries (without overwriting existing entries).
        /// </summary>
        /// <typeparam name="TKey">Dictionary key type</typeparam>
        /// <typeparam name="TValue">Dictionary value type</typeparam>
        /// <param name="dict">The dictionary to merge into.</param>
        /// <param name="dictToMerge">The dictionary to merge from.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void MergeWith<TKey, TValue>(this IDictionary<TKey, TValue> dict, IDictionary<TKey, TValue> dictToMerge)
        {
            foreach (var kvp in dictToMerge)
            {
                dict.AddIfNotExists(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Merge two dictionaries (without overwriting existing entries).
        /// </summary>
        /// <typeparam name="TKey">Dictionary key type</typeparam>
        /// <typeparam name="TValue">Dictionary value type</typeparam>
        /// <param name="dict">The dictionary to merge into.</param>
        /// <param name="dictToMerge">The dictionary to merge from.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void MergeWith<TKey, TValue>(this IDictionary<TKey, TValue> dict, IReadOnlyDictionary<TKey, TValue> dictToMerge)
        {
            foreach (var kvp in dictToMerge)
            {
                dict.AddIfNotExists(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Add key to a dictionary only if it doesn't exist.
        /// </summary>
        /// <typeparam name="TKey">Dictionary key type.</typeparam>
        /// <typeparam name="TValue">Dictionary value type.</typeparam>
        /// <param name="dict">The dictionary to add to.</param>
        /// <param name="key">Key of the data to add.</param>
        /// <param name="val">Value of the data to add.</param>
        /// <exception cref="ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentException">An element with the same key already exists in the <see cref="IDictionary{TKey,TValue}" />.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddIfNotExists<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue val)
        {
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, val);
            }
        }

        /// <summary>
        /// Convert a dictionary into ExpandoObject.
        /// </summary>
        /// <typeparam name="TValue">Dictionary value type.</typeparam>
        /// <param name="dict">Dictionary with the fields to turn into <see cref="ExpandoObject"/>.</param>
        /// <returns>The resulting <see cref="ExpandoObject"/>.</returns>
        internal static dynamic ToExpando<TValue>(this IReadOnlyDictionary<string, TValue> dict)
        {
            var result = (IDictionary<string, object>)new ExpandoObject()!;

            foreach (var kvp in dict)
            {
                result.Add(kvp.Key, kvp.Value);
            }

            return result;
        }

        /// <summary>
        /// Convert a dictionary into ExpandoObject.
        /// </summary>
        /// <typeparam name="TValue">Dictionary value type.</typeparam>
        /// <param name="dict">Dictionary with the fields to turn into <see cref="ExpandoObject"/>.</param>
        /// <returns>The resulting <see cref="ExpandoObject"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static dynamic ToExpando<TValue>(this IDictionary<string, TValue> dict) =>
            ToExpando((IReadOnlyDictionary<string, TValue>)dict);

        /// <summary>
        /// Either add or set a key in the dictionary (convenience!).
        /// </summary>
        /// <typeparam name="TKey">Dictionary key type.</typeparam>
        /// <typeparam name="TValue">Dictionary value type.</typeparam>
        /// <param name="dict">The dictionary to operate on.</param>
        /// <param name="key">The key to look for.</param>
        /// <param name="mutator">A lambda that yields a value that will be either set or added to the dictionary.</param>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/></exception>
        /// <exception cref="Exception">The mutator delegate callback may throw an exception.</exception>
        /// <exception cref="ArgumentException">An element with the same key already exists in the <see cref="Dictionary{TKey, TValue}" />.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddOrSet<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, Func<TValue, TValue> mutator)
            where TKey : notnull
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (dict.ContainsKey(key))
            {
                dict[key] = mutator(dict[key]);
            }
            else
            {
                dict.Add(key, mutator(default!));
            }
        }
    }
}
