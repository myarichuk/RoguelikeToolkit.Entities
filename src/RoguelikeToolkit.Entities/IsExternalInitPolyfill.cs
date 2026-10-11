#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Compiler support type for <c>init</c> accessors and records on netstandard2.1 (Unity). The compiler matches it by
    /// name; internal so it never clashes with the BCL type on newer targets.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
#endif
