namespace RoguelikeToolkit.Entities.Extensions
{
    /// <summary>
    /// A single null-guard helper so call sites don't repeat <c>#if NET5_0_OR_GREATER</c> blocks.
    /// Behavior is identical on every target: throws <see cref="ArgumentNullException"/> on null.
    /// </summary>
    internal static class ArgumentGuard
    {
        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> when <paramref name="argument"/> is null.
        /// </summary>
        /// <param name="argument">value to check for null.</param>
        /// <param name="paramName">name of the guarded parameter.</param>
        /// <exception cref="ArgumentNullException"><paramref name="argument"/> is <see langword="null"/>.</exception>
        public static void ThrowIfNull(object? argument, string paramName)
        {
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(argument, paramName);
#else
            if (argument == null)
            {
                throw new ArgumentNullException(paramName);
            }
#endif
        }
    }
}
