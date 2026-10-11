using System.Collections.Concurrent;

namespace RoguelikeToolkit.Entities
{
    /// <summary>
    /// Process-wide parsers that turn template strings into component member values of non-primitive types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Template values convert in this order: already assignable; a parser registered here for the member type
    /// (string values only); enums (name, case-insensitive, or underlying number); primitives, <see cref="string"/> and
    /// <see cref="decimal"/> (invariant culture, range-checked, fractional values never truncated into integers);
    /// <see cref="Guid"/>, <see cref="TimeSpan"/>, <see cref="DateTime"/>, <see cref="DateTimeOffset"/>,
    /// <see cref="Uri"/> and <see cref="Version"/> from strings. Anything else fails with
    /// <see cref="InvalidCastException"/> naming the member type, so a missing parser is found at the first spawn.
    /// </para>
    /// <para>
    /// <see cref="Dice"/> is registered out of the box. Scripting packages register their script types through
    /// <see cref="Register{T}(Func{string, T})"/> as well. A parser runs on every spawn that sets the member: cache
    /// inside it when the parsed value is immutable and parsing is not trivial (the built-in dice parser does).
    /// </para>
    /// </remarks>
    public static class TemplateValueParsers
    {
        private static readonly ConcurrentDictionary<Type, Func<string, object>> Parsers = new();
        private static readonly ConcurrentDictionary<string, Dice> DiceCache = new(StringComparer.Ordinal);

        static TemplateValueParsers() =>
            Register(static source => DiceCache.GetOrAdd(source, static s => Dice.Parse(s)));

        /// <summary>
        /// Registers (or replaces) the parser for template strings assigned to members of type <typeparamref name="T"/>
        /// (and <c>Nullable&lt;T&gt;</c>). Thread-safe; registering again replaces the previous parser.
        /// </summary>
        /// <typeparam name="T">member type the parser produces</typeparam>
        /// <param name="parse">parser; throw (typically <see cref="FormatException"/>) on invalid input, never return null</param>
        /// <exception cref="ArgumentNullException"><paramref name="parse"/> is null</exception>
        public static void Register<T>(Func<string, T> parse)
            where T : notnull
        {
            if (parse == null)
            {
                throw new ArgumentNullException(nameof(parse));
            }

            Parsers[typeof(T)] = source => parse(source);
        }

        /// <summary>
        /// Removes the parser for <typeparamref name="T"/>, if any.
        /// </summary>
        /// <typeparam name="T">member type</typeparam>
        /// <returns>true if a parser was removed</returns>
        public static bool Unregister<T>() => Parsers.TryRemove(typeof(T), out _);

        /// <summary>
        /// Checks whether a parser is registered for <paramref name="type"/>.
        /// </summary>
        /// <param name="type">member type</param>
        /// <returns>true if template strings can be parsed into <paramref name="type"/></returns>
        public static bool IsRegistered(Type type) => type != null && Parsers.ContainsKey(type);

        /// <summary>
        /// Looks up the parser for <paramref name="type"/>.
        /// </summary>
        /// <param name="type">member type (already unwrapped from <c>Nullable&lt;T&gt;</c>)</param>
        /// <param name="parse">the parser</param>
        /// <returns>true if found</returns>
        internal static bool TryGet(Type type, out Func<string, object> parse) => Parsers.TryGetValue(type, out parse!);
    }
}
