using System.Collections.Concurrent;
using DefaultEcs;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// Typed bridge from a runtime <see cref="Type"/> to DefaultEcs' generic component API.
    /// One closed <see cref="ComponentSetter{T}"/> per component type, cached for the process: after the
    /// first spawn of a type, setting a component is a virtual call and an unbox, with no
    /// <c>MethodInfo.Invoke</c>, argument arrays or runtime code generation.
    /// </summary>
    /// <remarks>
    /// AOT: the instance is created with <see cref="Type.MakeGenericType"/>, which IL2CPP (Unity 2022.1+, full
    /// generic sharing) and Mono support for any type argument. Runtimes that cannot instantiate generics over
    /// value types at runtime (e.g. NativeAOT, older IL2CPP) need the closed type to exist at compile time:
    /// call <see cref="AotComponentRegistry.Register{T}"/> once per component type at startup.
    /// </remarks>
    internal abstract class ComponentSetter
    {
        private static readonly ConcurrentDictionary<Type, ComponentSetter> Cache = new();

        /// <summary>
        /// Sets a regular (per entity) component.
        /// </summary>
        /// <param name="entity">entity to set the component on</param>
        /// <param name="componentInstance">boxed component of the setter's type</param>
        public abstract void Set(in Entity entity, object componentInstance);

        /// <summary>
        /// Sets a global component: the world's copy is set when absent (first write wins), then the entity is linked to it.
        /// </summary>
        /// <param name="world">world that owns the shared copy</param>
        /// <param name="entity">entity to link</param>
        /// <param name="componentInstance">boxed component of the setter's type</param>
        /// <returns>true if this call set the world's copy, false if one already existed</returns>
        public abstract bool SetGlobal(World world, in Entity entity, object componentInstance);

        /// <summary>
        /// Returns the cached setter for <paramref name="componentType"/>, creating it on first use.
        /// </summary>
        /// <param name="componentType">component type</param>
        /// <returns>setter bound to the component type</returns>
        internal static ComponentSetter For(Type componentType) =>
            Cache.GetOrAdd(
                componentType,
                static type => (ComponentSetter)Activator.CreateInstance(typeof(ComponentSetter<>).MakeGenericType(type))!);

        /// <summary>
        /// Registers a setter created from statically known generic code (AOT-safe).
        /// </summary>
        /// <typeparam name="T">component type</typeparam>
        internal static void Register<T>() => Cache.TryAdd(typeof(T), new ComponentSetter<T>());
    }
}
