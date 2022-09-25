using Microsoft.Extensions.ObjectPool;

namespace RoguelikeToolkit.Entities
{
    /// <summary>
    /// A singleton class that provides an instance of the <see cref="Microsoft.Extensions.ObjectPool.ObjectPoolProvider"/>.
    /// </summary>
    internal static class ObjectPoolProvider
    {
        private static readonly Lazy<DefaultObjectPoolProvider> TheProvider = new(() => new DefaultObjectPoolProvider());

        /// <summary>
        /// Gets the instance of <see cref="Microsoft.Extensions.ObjectPool.ObjectPoolProvider"/>.
        /// </summary>
        public static Microsoft.Extensions.ObjectPool.ObjectPoolProvider Instance => TheProvider.Value;
    }
}
