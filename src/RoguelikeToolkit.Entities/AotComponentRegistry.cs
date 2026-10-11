using System.Diagnostics.CodeAnalysis;
using RoguelikeToolkit.Entities.Factory;

namespace RoguelikeToolkit.Entities
{
    /// <summary>
    /// Ahead-of-time hints for component types.
    /// </summary>
    /// <remarks>
    /// Not needed on JIT runtimes (CoreCLR, Mono) or on Unity IL2CPP 2022.1+, where the factory instantiates its
    /// per-type component accessors on demand. On AOT runtimes that cannot create generic instantiations over value types at
    /// runtime (NativeAOT, IL2CPP before 2022.1), call <see cref="Register{T}"/> once per component type at startup so the
    /// closed generic code is compiled in. Registering is idempotent and safe on every runtime.
    /// Member types of nested component properties (a struct/class property populated from a nested mapping) are
    /// reached through reflection only: keep them from being stripped too (Unity: <c>link.xml</c> or <c>[Preserve]</c>).
    /// </remarks>
    public static class AotComponentRegistry
    {
        // Everything the factory reflects over on a component type: interfaces (IValueComponent<T> detection),
        // properties (member writes), fields (get-only auto-property backing fields), constructors (instantiation).
        private const DynamicallyAccessedMemberTypes ReflectedMembers =
            DynamicallyAccessedMemberTypes.Interfaces |
            DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties |
            DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields |
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

        /// <summary>
        /// Pre-creates the component accessor for <typeparamref name="T"/> from statically known generic code, and tells
        /// trimming linkers that understand <c>DynamicallyAccessedMembers</c> (ILLink, NativeAOT) to keep the members
        /// the factory reaches through reflection.
        /// </summary>
        /// <typeparam name="T">component type that templates may reference</typeparam>
        public static void Register<[DynamicallyAccessedMembers(ReflectedMembers)] T>() => ComponentAccessor.Register<T>();
    }
}
