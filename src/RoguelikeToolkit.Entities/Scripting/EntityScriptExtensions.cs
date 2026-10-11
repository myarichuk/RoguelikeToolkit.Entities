using DefaultEcs;
using Jint.Native;
using Jint.Runtime.Interop;
using RoguelikeToolkit.Entities.Factory;

namespace RoguelikeToolkit.Entities.Scripting
{
    /// <summary>
    /// Non-generic component access for scripts (JavaScript cannot call <c>Entity.Get&lt;T&gt;()</c>), registered as
    /// extension methods on every <see cref="ScriptHost"/>: scripts write <c>entity.GetComponent(Health)</c> with
    /// <c>Health</c> exposed through <see cref="ScriptHostOptions.ReferenceTypes"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The type parameter is a <see cref="JsValue"/>, not a <see cref="Type"/>: a Jint type reference is a JS function,
    /// and Jint's binder converts it to a delegate rather than to <see cref="Type"/>, so a <see cref="Type"/> parameter
    /// never binds from script. Internal so these script-only overloads do not show up on <see cref="Entity"/> in C#.
    /// </para>
    /// <para>
    /// Dispatches through the factory's cached closed <see cref="ComponentAccessor{T}"/> (no <c>MakeGenericMethod</c> +
    /// <c>Invoke</c> per call). On runtimes without runtime generic instantiation over value types (NativeAOT, IL2CPP
    /// before 2022.1) component types must be registered via <see cref="AotComponentRegistry.Register{T}"/>.
    /// Value-type components come back as a copy: write it back with <c>SetComponent</c>.
    /// </para>
    /// </remarks>
    internal static class EntityScriptExtensions
    {
        /// <summary>
        /// Checks whether the entity has a component of the referenced type.
        /// </summary>
        /// <param name="entity">entity</param>
        /// <param name="componentType">type reference (a <see cref="ScriptHostOptions.ReferenceTypes"/> global)</param>
        /// <returns>true if present</returns>
        public static bool HasComponent(this Entity entity, JsValue componentType) =>
            ComponentAccessor.For(ResolveType(componentType)).Has(entity);

        /// <summary>
        /// Returns the entity's component of the referenced type (a copy for value types).
        /// </summary>
        /// <param name="entity">entity</param>
        /// <param name="componentType">type reference (a <see cref="ScriptHostOptions.ReferenceTypes"/> global)</param>
        /// <returns>the component</returns>
        public static object? GetComponent(this Entity entity, JsValue componentType) =>
            ComponentAccessor.For(ResolveType(componentType)).Get(entity);

        /// <summary>
        /// Sets (adds or replaces) the entity's component of the referenced type.
        /// </summary>
        /// <param name="entity">entity</param>
        /// <param name="componentType">type reference (a <see cref="ScriptHostOptions.ReferenceTypes"/> global)</param>
        /// <param name="value">component instance of that type</param>
        public static void SetComponent(this Entity entity, JsValue componentType, object value)
        {
            var type = ResolveType(componentType);
            if (value == null || !type.IsInstanceOfType(value))
            {
                throw new ArgumentException(
                    $"Expected a component of type '{type.FullName}', got '{value?.GetType().FullName ?? "null"}'.",
                    nameof(value));
            }

            ComponentAccessor.For(type).Set(entity, value);
        }

        /// <summary>
        /// Removes the entity's component of the referenced type (no-op when absent).
        /// </summary>
        /// <param name="entity">entity</param>
        /// <param name="componentType">type reference (a <see cref="ScriptHostOptions.ReferenceTypes"/> global)</param>
        public static void RemoveComponent(this Entity entity, JsValue componentType) =>
            ComponentAccessor.For(ResolveType(componentType)).Remove(entity);

        private static Type ResolveType(JsValue componentType) =>
            componentType is IObjectWrapper { Target: Type type }
                ? type
                : throw new ArgumentException(
                    $"Expected a CLR type reference (expose component types via {nameof(ScriptHostOptions)}.{nameof(ScriptHostOptions.ReferenceTypes)}), got '{componentType}'.",
                    nameof(componentType));
    }
}
