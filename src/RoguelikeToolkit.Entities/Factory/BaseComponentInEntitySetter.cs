using DefaultEcs;

// ReSharper disable UnusedMember.Global
namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// An base class for abstracting component setting logic
    /// </summary>
    internal abstract class BaseComponentInEntitySetter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseComponentInEntitySetter"/> class
        /// </summary>
        /// <param name="world">entity "world" to operate on</param>
        protected BaseComponentInEntitySetter(World world) => World = world;

        /// <summary>
        /// Gets the value of the entity "world" to operate on
        /// </summary>
        protected World World { get; }

        /// <summary>
        /// Can the component setter operate on the specified component type?
        /// </summary>
        /// <param name="componentType">type of the component to operate on</param>
        /// <returns>true if the setter can operate on the component type, false otherwise</returns>
        internal abstract bool CanSetComponent(Type componentType);

        /// <summary>
        /// Set the component in the entity according to the defined "business" logic
        /// </summary>
        /// <param name="entity">the entity to set it's component</param>
        /// <param name="componentType">the type of the component to set</param>
        /// <param name="componentInstance">the instance of the component to set</param>
        internal abstract void SetComponent(in Entity entity, Type componentType, object componentInstance);
    }
}
