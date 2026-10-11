using System.Globalization;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// Converts template scalars (what YamlDotNet hands back: strings, numbers, booleans) to component member types.
    /// The rules are listed on <see cref="TemplateValueParsers"/>.
    /// </summary>
    /// <remarks>
    /// Uses only non-generic BCL APIs (<see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>,
    /// <see cref="Enum.Parse(Type, string, bool)"/>, the BCL <c>Parse</c> methods), so it needs no runtime generic
    /// instantiation and behaves the same on JIT and AOT runtimes (IL2CPP, NativeAOT). It never loses data silently:
    /// out-of-range numbers throw <see cref="OverflowException"/> and fractional numbers assigned to integral members
    /// throw <see cref="FormatException"/> instead of being truncated, rounded or saturated.
    /// </remarks>
    internal static class TemplateValueConverter
    {
        /// <summary>
        /// Converts <paramref name="value"/> to <paramref name="destType"/>.
        /// </summary>
        /// <param name="value">source value (non-null)</param>
        /// <param name="destType">destination type</param>
        /// <returns>the converted value</returns>
        /// <exception cref="FormatException">a string cannot be parsed as the destination type, or a fractional number targets an integral type</exception>
        /// <exception cref="OverflowException">a number does not fit the destination type</exception>
        /// <exception cref="InvalidCastException">no conversion exists from the value's type to <paramref name="destType"/></exception>
        public static object Convert(object value, Type destType)
        {
            if (destType.IsInstanceOfType(value))
            {
                return value;
            }

            var targetType = Nullable.GetUnderlyingType(destType) ?? destType;
            var sourceType = value.GetType();

            if (value is string text)
            {
                if (TemplateValueParsers.TryGet(targetType, out var parse))
                {
                    return parse(text) ?? throw new InvalidOperationException(
                        $"The template value parser registered for '{targetType.FullName}' returned null for '{text}'.");
                }

                if (TryParseWellKnown(text, targetType, out var parsed))
                {
                    return parsed;
                }
            }

            if (targetType.IsEnum)
            {
                if (value is string enumText)
                {
                    return Enum.Parse(targetType, enumText, ignoreCase: true);
                }

                if (IsIntegral(sourceType))
                {
                    return Enum.ToObject(targetType, value);
                }

                throw Unsupported(value, destType);
            }

            if (IsSimple(targetType) && (value is string || IsSimple(sourceType)))
            {
                if (IsIntegral(targetType) && IsFractional(sourceType) && !IsWholeNumber(value))
                {
                    throw new FormatException(
                        $"'{System.Convert.ToString(value, CultureInfo.InvariantCulture)}' has a fractional part and cannot be assigned to '{targetType.FullName}' without losing data.");
                }

                return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }

            throw Unsupported(value, destType);
        }

        private static bool TryParseWellKnown(string text, Type targetType, out object result)
        {
            var invariant = CultureInfo.InvariantCulture;
            if (targetType == typeof(Guid))
            {
                result = Guid.Parse(text);
            }
            else if (targetType == typeof(TimeSpan))
            {
                result = TimeSpan.Parse(text, invariant);
            }
            else if (targetType == typeof(DateTime))
            {
                result = DateTime.Parse(text, invariant, DateTimeStyles.RoundtripKind);
            }
            else if (targetType == typeof(DateTimeOffset))
            {
                result = DateTimeOffset.Parse(text, invariant, DateTimeStyles.None);
            }
            else if (targetType == typeof(Uri))
            {
                result = new Uri(text, UriKind.RelativeOrAbsolute);
            }
            else if (targetType == typeof(Version))
            {
                result = Version.Parse(text);
            }
            else
            {
                result = null!;
                return false;
            }

            return true;
        }

        private static InvalidCastException Unsupported(object value, Type destType) =>
            new(
                $"Cannot convert template value '{value}' ({value.GetType().FullName}) to '{destType.FullName}'. " +
                $"Register a parser with {nameof(TemplateValueParsers)}.{nameof(TemplateValueParsers.Register)}<T>() for custom types.");

        private static bool IsWholeNumber(object value) => value switch
        {
            float f => Math.Floor(f) == f,
            double d => Math.Floor(d) == d,
            decimal m => decimal.Truncate(m) == m,
            _ => true,
        };

        private static bool IsSimple(Type type) =>
            type.IsPrimitive || type == typeof(string) || type == typeof(decimal);

        private static bool IsFractional(Type type) =>
            type == typeof(float) || type == typeof(double) || type == typeof(decimal);

        private static bool IsIntegral(Type type) =>
            type == typeof(byte) || type == typeof(sbyte) ||
            type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) ||
            type == typeof(long) || type == typeof(ulong);
    }
}
