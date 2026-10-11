using System.Collections.Concurrent;
using System.Reflection;
using RoguelikeToolkit.Entities.Components;

namespace RoguelikeToolkit.Entities.Extensions
{
    /// <summary>
    /// A helper class that contains <see cref="System.Type"/> related functions.
    /// </summary>
    internal static class TypeExtensions
    {
        // Attribute lookups allocate on every call; component types are fixed for the process, so resolve once.
        private static readonly ConcurrentDictionary<Type, ComponentAttribute?> ComponentAttributeCache = new();

        /// <summary>
        /// Returns the <see cref="ComponentAttribute"/> of a type (inherited included), or null if it has none.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <returns>The attribute instance, or <see langword="null"/>.</returns>
        public static ComponentAttribute? GetComponentAttribute(this Type type) =>
            ComponentAttributeCache.GetOrAdd(type, static t => t.GetCustomAttribute<ComponentAttribute>(inherit: true));

        /// <summary>
        /// Return true if a type is marked as a global (world-shared) component.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <returns><see langword="true" /> for global components.</returns>
        public static bool IsGlobalComponentType(this Type type) =>
            type.GetComponentAttribute()?.IsGlobal ?? false;

        /// <summary>
        /// Returns the closed <see cref="IValueComponent{TValue}"/> interface a type implements, or null.
        /// Compared by generic definition (not by name), which also keeps the interface visible to AOT linkers.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <returns>The closed interface, or <see langword="null"/>.</returns>
        public static Type? GetValueComponentInterface(this Type type)
        {
            foreach (var implemented in type.GetInterfaces())
            {
                if (implemented.IsGenericType && implemented.GetGenericTypeDefinition() == typeof(IValueComponent<>))
                {
                    return implemented;
                }
            }

            return null;
        }

        /// <summary>
        /// Return true if a type implements <see cref="IValueComponent{TValue}"/>.
        /// </summary>
        /// <param name="type">Type to check if it implements the <see cref="IValueComponent{TValue}"/> interface.</param>
        /// <returns>Returns <see langword="true" /> if <paramref name="type"/> implements the <see cref="IValueComponent{TValue}"/> interface, <see langword="false" /> otherwise.</returns>
        public static bool IsValueComponentType(this Type type)
        {
            try
            {
                return GetValueComponentInterface(type) != null;
            }
            catch (TargetInvocationException)
            {
                return false;
            }
        }
    }
}
