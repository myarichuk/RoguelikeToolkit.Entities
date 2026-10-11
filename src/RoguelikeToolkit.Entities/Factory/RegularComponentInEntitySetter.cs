using DefaultEcs;
using RoguelikeToolkit.Entities.Extensions;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// A class used to set "regular", non-global components in the entity
    /// </summary>
    internal class RegularComponentInEntitySetter : BaseComponentInEntitySetter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RegularComponentInEntitySetter"/> class
        /// </summary>
        /// <param name="world">entity "world" to operate on</param>
        public RegularComponentInEntitySetter(World world)
            : base(world)
        {
        }

        /// <inheritdoc />
        internal override bool CanSetComponent(Type componentType) =>
            !componentType.IsGlobalComponentType();

        /// <inheritdoc />
        internal override void SetComponent(in Entity entity, Type componentType, object componentInstance) =>
            ComponentAccessor.For(componentType).Set(entity, componentInstance);
    }
}
