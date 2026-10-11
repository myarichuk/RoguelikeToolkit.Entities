namespace RoguelikeToolkit.Entities
{
    /// <summary>
    /// Bounded, lock-free pool of reusable instances (traversal queues, stacks, visited sets).
    /// </summary>
    /// <remarks>
    /// One fast slot plus a fixed array of slots, each claimed with a single <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/>:
    /// no locks, no allocation on <see cref="Rent"/>/<see cref="Return"/> in steady state. The pool is bounded, so a
    /// burst of concurrent renters allocates the overflow and <see cref="Return"/> drops it instead of growing without
    /// limit. The pool does not reset instances: callers clear what they return (a returned container must not keep
    /// entity or template references alive).
    /// </remarks>
    /// <typeparam name="T">pooled type</typeparam>
    internal sealed class Pool<T>
        where T : class, new()
    {
        private readonly T?[] _slots;
        private T? _fast;

        /// <summary>
        /// Initializes a new instance of the <see cref="Pool{T}"/> class.
        /// </summary>
        /// <param name="capacity">instances retained beyond the fast slot; defaults to two per processor</param>
        public Pool(int capacity = 0) =>
            _slots = new T?[capacity > 0 ? capacity : Environment.ProcessorCount * 2];

        /// <summary>
        /// Takes a pooled instance, or creates one when the pool is empty.
        /// </summary>
        /// <returns>an instance owned by the caller until <see cref="Return"/></returns>
        public T Rent()
        {
            var item = _fast;
            if (item != null && Interlocked.CompareExchange(ref _fast, null, item) == item)
            {
                return item;
            }

            var slots = _slots;
            for (var i = 0; i < slots.Length; i++)
            {
                item = slots[i];
                if (item != null && Interlocked.CompareExchange(ref slots[i], null, item) == item)
                {
                    return item;
                }
            }

            return new T();
        }

        /// <summary>
        /// Hands an instance back. When every slot is taken the instance is dropped for the GC.
        /// </summary>
        /// <param name="item">instance obtained from <see cref="Rent"/>, already cleared by the caller</param>
        public void Return(T item)
        {
            if (_fast == null && Interlocked.CompareExchange(ref _fast, item, null) == null)
            {
                return;
            }

            var slots = _slots;
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null && Interlocked.CompareExchange(ref slots[i], item, null) == null)
                {
                    return;
                }
            }
        }
    }
}
