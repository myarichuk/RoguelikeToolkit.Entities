using System.Runtime.CompilerServices;

namespace RoguelikeToolkit.Entities.Extensions
{
    /// <summary>
    /// Reference-equality comparer for reference types (equivalent to the runtime-provided
    /// <c>ReferenceEqualityComparer</c> which is not available on all target frameworks).
    /// </summary>
    /// <typeparam name="T">reference type to compare.</typeparam>
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        /// <summary>
        /// Gets the singleton instance of the comparer.
        /// </summary>
        public static ReferenceEqualityComparer<T> Instance { get; } = new();

        /// <inheritdoc/>
        public bool Equals(T? x, T? y) =>
            ReferenceEquals(x, y);

        /// <inheritdoc/>
        public int GetHashCode(T obj) =>
            RuntimeHelpers.GetHashCode(obj);
    }
}
