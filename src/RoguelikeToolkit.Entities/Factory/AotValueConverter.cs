using System.Globalization;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// Conversion fast path for the values templates actually contain: already-assignable values, numbers,
    /// strings, booleans, enums and registered string-parsed types (dice, scripts).
    /// Uses only non-generic BCL APIs (<see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>,
    /// <see cref="Enum.Parse(Type, string, bool)"/>), so it needs no runtime generic instantiation and works the same on
    /// JIT and AOT runtimes (IL2CPP, NativeAOT). Anything it does not handle is left to the general converter.
    /// </summary>
    internal static class AotValueConverter
    {
        /// <summary>
        /// Tries to convert <paramref name="value"/> to <paramref name="destType"/>.
        /// </summary>
        /// <param name="value">source value (non-null)</param>
        /// <param name="destType">destination type</param>
        /// <param name="stringConverters">custom string parsers keyed by destination type</param>
        /// <param name="result">converted value</param>
        /// <returns>true if handled here; false means "not a fast-path case", not "conversion failed"</returns>
        /// <exception cref="FormatException">a string cannot be parsed as the destination type</exception>
        /// <exception cref="OverflowException">a number does not fit the destination type</exception>
        public static bool TryConvert(
            object value,
            Type destType,
            IReadOnlyDictionary<Type, Func<string, object>> stringConverters,
            out object? result)
        {
            if (destType.IsInstanceOfType(value))
            {
                result = value;
                return true;
            }

            var targetType = Nullable.GetUnderlyingType(destType) ?? destType;

            if (value is string text && stringConverters.TryGetValue(targetType, out var parse))
            {
                result = parse(text);
                return true;
            }

            if (targetType.IsEnum)
            {
                if (value is string enumText)
                {
                    result = Enum.Parse(targetType, enumText, ignoreCase: true);
                    return true;
                }

                if (IsIntegral(value.GetType()))
                {
                    result = Enum.ToObject(targetType, value);
                    return true;
                }

                result = null;
                return false;
            }

            if (!IsSimple(targetType) || value is not IConvertible)
            {
                result = null;
                return false;
            }

            var sourceType = value.GetType();

            // fractional -> integral: rounding vs truncation semantics differ between converters, leave it to the general one
            if (IsIntegral(targetType) && IsFractional(sourceType))
            {
                result = null;
                return false;
            }

            if (value is string || targetType == typeof(string) || IsNumeric(sourceType) || sourceType == typeof(bool) || sourceType == typeof(char))
            {
                result = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IsSimple(Type type) =>
            type.IsPrimitive || type == typeof(string) || type == typeof(decimal);

        private static bool IsNumeric(Type type) => IsIntegral(type) || IsFractional(type);

        private static bool IsFractional(Type type) =>
            type == typeof(float) || type == typeof(double) || type == typeof(decimal);

        private static bool IsIntegral(Type type) =>
            type == typeof(byte) || type == typeof(sbyte) ||
            type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) ||
            type == typeof(long) || type == typeof(ulong);
    }
}
