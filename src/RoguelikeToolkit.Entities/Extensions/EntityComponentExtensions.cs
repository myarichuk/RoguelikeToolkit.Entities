using DefaultEcs;
using RoguelikeToolkit.Entities.Factory;

namespace RoguelikeToolkit.Entities.Extensions
{
    /// <summary>
    /// Component access by runtime <see cref="Type"/>, for callers that do not know the component type at compile time
    /// (scripting bridges, editors, debug tooling).
    /// </summary>
    /// <remarks>
    /// Dispatches through a cached closed accessor per component type: after the first use of a type a call is a virtual
    /// call plus a (un)box, with no <c>MethodInfo.Invoke</c> and no runtime code generation. On runtimes without runtime
    /// generic instantiation over value types (NativeAOT, IL2CPP before 2022.1) register component types via
    /// <see cref="AotComponentRegistry.Register{T}"/>. Value-type components come back as a boxed copy.
    /// </remarks>
    public static class EntityComponentExtensions
    {
        /// <summary>
        /// Checks whether the entity has a component of <paramref name="componentType"/>.
        /// </summary>
        /// <param name="entity">entity</param>
        /// <param name="componentType">component type</param>
        /// <returns>true if present</returns>
        /// <exception cref="ArgumentNullException"><paramref name="componentType"/> is null</exception>
        public static bool HasComponent(this Entity entity, Type componentType) =>
            For(componentType).Has(entity);

        /// <summary>
        /// Returns the entity's component of <paramref name="componentType"/> (a boxed copy for value types).
        /// </summary>
        /// <param name="entity">entity</param>
        /// <param name="componentType">component type</param>
        /// <returns>the component</returns>
        /// <exception cref="ArgumentNullException"><paramref name="componentType"/> is null</exception>
        /// <exception cref="InvalidOperationException">the entity does not have the component</exception>
        public static object? GetComponent(this Entity entity, Type componentType) =>
            For(componentType).Get(entity);

        /// <summary>
        /// Sets (adds or replaces) the entity's component of <paramref name="componentType"/>.
        /// </summary>
        /// <param name="entity">entity</param>
        /// <param name="componentType">component type</param>
        /// <param name="component">component instance of exactly that type (or a subtype, for reference types)</param>
        /// <exception cref="ArgumentNullException"><paramref name="componentType"/> is null</exception>
        /// <exception cref="ArgumentException"><paramref name="component"/> is null or not a <paramref name="componentType"/></exception>
        public static void SetComponent(this Entity entity, Type componentType, object component)
        {
            var accessor = For(componentType);
            if (component == null || !componentType.IsInstanceOfType(component))
            {
                throw new ArgumentException(
                    $"Expected a component of type '{componentType.FullName}', got '{component?.GetType().FullName ?? "null"}'.",
                    nameof(component));
            }

            accessor.Set(entity, component);
        }

        /// <summary>
        /// Removes the entity's component of <paramref name="componentType"/> (no-op when absent).
        /// </summary>
        /// <param name="entity">entity</param>
        /// <param name="componentType">component type</param>
        /// <exception cref="ArgumentNullException"><paramref name="componentType"/> is null</exception>
        public static void RemoveComponent(this Entity entity, Type componentType) =>
            For(componentType).Remove(entity);

        private static ComponentAccessor For(Type componentType) =>
            ComponentAccessor.For(componentType ?? throw new ArgumentNullException(nameof(componentType)));
    }
}
