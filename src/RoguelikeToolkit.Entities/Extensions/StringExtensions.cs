using System.Runtime.CompilerServices;

namespace RoguelikeToolkit.Entities.Extensions
{
    /// <summary>
    /// A utility class with various string related helper methods
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Trim strings by removing specified amount of characters from the beginning and an end of a string
        /// </summary>
        /// <param name="str">A string to operate on</param>
        /// <param name="before">How much characters to trim from the beginning</param>
        /// <param name="after">How much characters to trim from the end</param>
        /// <returns>Trimmed string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Trim(this string str, int before, int after = 0) =>
            str[before..^after];
    }
}
