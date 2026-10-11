#if !NET5_0_OR_GREATER
// netstandard2.1 lacks these; trimmers (ILLink/NativeAOT) match them by full name, so an internal copy works.
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Polyfill: marks members a trimmer must keep because they are accessed through reflection.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter |
        AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Method |
        AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct,
        Inherited = false)]
    internal sealed class DynamicallyAccessedMembersAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicallyAccessedMembersAttribute"/> class.
        /// </summary>
        /// <param name="memberTypes">member kinds to keep</param>
        public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes) => MemberTypes = memberTypes;

        /// <summary>
        /// Gets the member kinds to keep.
        /// </summary>
        public DynamicallyAccessedMemberTypes MemberTypes { get; }
    }

    /// <summary>
    /// Polyfill: member kinds for <see cref="DynamicallyAccessedMembersAttribute"/> (values match the BCL enum).
    /// </summary>
    [Flags]
    internal enum DynamicallyAccessedMemberTypes
    {
        /// <summary>No members.</summary>
        None = 0,

        /// <summary>Public parameterless constructor.</summary>
        PublicParameterlessConstructor = 0x0001,

        /// <summary>Public constructors.</summary>
        PublicConstructors = 0x0002 | PublicParameterlessConstructor,

        /// <summary>Non-public constructors.</summary>
        NonPublicConstructors = 0x0004,

        /// <summary>Public fields.</summary>
        PublicFields = 0x0020,

        /// <summary>Non-public fields.</summary>
        NonPublicFields = 0x0040,

        /// <summary>Public properties.</summary>
        PublicProperties = 0x0200,

        /// <summary>Non-public properties.</summary>
        NonPublicProperties = 0x0400,

        /// <summary>Implemented interfaces.</summary>
        Interfaces = 0x2000,
    }
}
#endif
