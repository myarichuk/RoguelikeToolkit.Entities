using DefaultEcs;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// Closed setter for one component type.
    /// </summary>
    /// <typeparam name="T">component type</typeparam>
    internal sealed class ComponentSetter<T> : ComponentSetter
    {
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
