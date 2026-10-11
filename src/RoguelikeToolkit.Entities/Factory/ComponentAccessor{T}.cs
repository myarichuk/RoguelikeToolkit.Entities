using DefaultEcs;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// Closed accessor for one component type.
    /// </summary>
    /// <typeparam name="T">component type</typeparam>
    internal sealed class ComponentAccessor<T> : ComponentAccessor
    {
        /// <inheritdoc />
        public override bool Has(in Entity entity) => entity.Has<T>();

        /// <inheritdoc />
        public override object? Get(in Entity entity) =>
            entity.Has<T>()
                ? entity.Get<T>()
                : throw new InvalidOperationException($"Entity does not have a component of type '{typeof(T).FullName}'.");

        /// <inheritdoc />
        public override void Remove(in Entity entity) => entity.Remove<T>();

        /// <inheritdoc />
        public override void Set(in Entity entity, object componentInstance) =>
            entity.Set((T)componentInstance);

        /// <inheritdoc />
        public override bool SetGlobal(World world, in Entity entity, object componentInstance)
        {
            var isFirstWrite = !world.Has<T>();
            if (isFirstWrite)
            {
                world.Set((T)componentInstance);
            }

            entity.SetSameAsWorld<T>();
            return isFirstWrite;
        }
    }
}
