#if !NET5_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Polyfill: keeps members of another type alive whenever the annotated member is kept.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    internal sealed class DynamicDependencyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicDependencyAttribute"/> class.
        /// </summary>
        /// <param name="memberTypes">member kinds to keep</param>
        /// <param name="type">type whose members to keep</param>
        public DynamicDependencyAttribute(DynamicallyAccessedMemberTypes memberTypes, Type type)
        {
            MemberTypes = memberTypes;
            Type = type;
        }

        /// <summary>
        /// Gets the member kinds to keep.
        /// </summary>
        public DynamicallyAccessedMemberTypes MemberTypes { get; }

        /// <summary>
        /// Gets the type whose members to keep.
        /// </summary>
        public Type? Type { get; }
    }
}
#endif
