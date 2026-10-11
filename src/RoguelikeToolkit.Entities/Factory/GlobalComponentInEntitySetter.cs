using DefaultEcs;
using RoguelikeToolkit.Entities.Extensions;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// A class used to set global components in the entity (components their instances would be shared between entities).
    /// Conflict semantics: first write wins. The world's copy is set by the first spawned entity that defines the
    /// component; later spawns with different values are ignored, so every entity observes the same shared instance
    /// (entities link to the world copy via <see cref="Entity.SetSameAsWorld{T}"/>, which snapshots the instance,
    /// hence overwriting the world copy later would leave earlier entities with stale references).
    /// </summary>
    internal class GlobalComponentInEntitySetter : BaseComponentInEntitySetter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalComponentInEntitySetter"/> class
        /// </summary>
        /// <param name="world">entity "world" to operate on</param>
        public GlobalComponentInEntitySetter(World world)
            : base(world)
        {
        }

        /// <inheritdoc/>
        internal override bool CanSetComponent(Type componentType) =>
            componentType.IsGlobalComponentType();

        /// <inheritdoc/>
        /// <remarks>First write wins: the world's copy is set only when absent, then the entity is linked to it.</remarks>
        internal override void SetComponent(in Entity entity, Type componentType, object componentInstance)
        {
            if (!ComponentAccessor.For(componentType).SetGlobal(World, entity, componentInstance))
            {
                EntityDiagnostics.Warn(
                    $"Global component '{componentType.FullName}' is already set on the world; the value from this spawn was ignored (first write wins). " +
                    "Remove the duplicate global component from the template if the warning is unexpected.");
            }
        }
    }
}
