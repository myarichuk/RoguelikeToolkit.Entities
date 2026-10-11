using System.Collections.Concurrent;
using DefaultEcs;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// Typed bridge from a runtime <see cref="Type"/> to DefaultEcs' generic component API.
    /// One closed <see cref="ComponentAccessor{T}"/> per component type, cached for the process: after the
    /// first use of a type, reading/writing a component is a virtual call and a (un)box, with no
    /// <c>MethodInfo.Invoke</c>, argument arrays or runtime code generation. Used by the entity factory
    /// (spawn) and by script interop (<c>entity.GetComponent(Type)</c> and friends).
    /// </summary>
    /// <remarks>
    /// AOT: the instance is created with <see cref="Type.MakeGenericType"/>, which IL2CPP (Unity 2022.1+, full
    /// generic sharing) and Mono support for any type argument. Runtimes that cannot instantiate generics over
    /// value types at runtime (e.g. NativeAOT, older IL2CPP) need the closed type to exist at compile time:
    /// call <see cref="AotComponentRegistry.Register{T}"/> once per component type at startup.
    /// </remarks>
    internal abstract class ComponentAccessor
    {
        private static readonly ConcurrentDictionary<Type, ComponentAccessor> Cache = new();

        /// <summary>
        /// Checks whether the entity has the component.
        /// </summary>
        /// <param name="entity">entity to check</param>
        /// <returns>true if the entity has the component</returns>
        public abstract bool Has(in Entity entity);

        /// <summary>
        /// Returns a boxed copy of the entity's component (value types) or the instance itself (reference types).
        /// </summary>
        /// <param name="entity">entity to read</param>
        /// <returns>the component</returns>
        /// <exception cref="InvalidOperationException">the entity does not have the component</exception>
        public abstract object? Get(in Entity entity);

        /// <summary>
        /// Removes the component from the entity (no-op when absent).
        /// </summary>
        /// <param name="entity">entity to modify</param>
        public abstract void Remove(in Entity entity);

        /// <summary>
        /// Sets a regular (per entity) component.
        /// </summary>
        /// <param name="entity">entity to set the component on</param>
        /// <param name="componentInstance">boxed component of the accessor's type</param>
        public abstract void Set(in Entity entity, object componentInstance);

        /// <summary>
        /// Sets a global component: the world's copy is set when absent (first write wins), then the entity is linked to it.
        /// </summary>
        /// <param name="world">world that owns the shared copy</param>
        /// <param name="entity">entity to link</param>
        /// <param name="componentInstance">boxed component of the accessor's type</param>
        /// <returns>true if this call set the world's copy, false if one already existed</returns>
        public abstract bool SetGlobal(World world, in Entity entity, object componentInstance);

        /// <summary>
        /// Returns the cached accessor for <paramref name="componentType"/>, creating it on first use.
        /// </summary>
        /// <param name="componentType">component type</param>
        /// <returns>accessor bound to the component type</returns>
        internal static ComponentAccessor For(Type componentType) =>
            Cache.GetOrAdd(
                componentType,
                static type => (ComponentAccessor)Activator.CreateInstance(typeof(ComponentAccessor<>).MakeGenericType(type))!);

        /// <summary>
        /// Registers an accessor created from statically known generic code (AOT-safe).
        /// </summary>
        /// <typeparam name="T">component type</typeparam>
        internal static void Register<T>() => Cache.TryAdd(typeof(T), new ComponentAccessor<T>());
    }
}
