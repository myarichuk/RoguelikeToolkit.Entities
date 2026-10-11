using System.Reflection;
using Jint;

namespace RoguelikeToolkit.Entities.Scripting.Jint
{
    /// <summary>
    /// Sandbox limits and interop surface of a <see cref="ScriptHost"/>.
    /// </summary>
    public sealed class ScriptHostOptions
    {
        /// <summary>
        /// Gets the maximum statements one execution may run; 0 disables the limit. Default 500.
        /// </summary>
        public int MaxStatements { get; init; } = 500;

        /// <summary>
        /// Gets the maximum call depth; 0 disables the limit. Default 1024.
        /// </summary>
        public int RecursionLimit { get; init; } = 1024;

        /// <summary>
        /// Gets the wall-clock limit of one execution; null disables it. Default null.
        /// </summary>
        public TimeSpan? Timeout { get; init; }

        /// <summary>
        /// Gets the per-execution allocation limit in bytes; 0 disables it. Default 0 (off).
        /// </summary>
        /// <remarks>
        /// Off by default on purpose: Jint checks it after every statement through
        /// <see cref="GC.GetAllocatedBytesForCurrentThread"/>, which costs a call per statement, and Unity IL2CPP's
        /// support for that API is not something to rely on. Enable it for untrusted (mod) scripts on CoreCLR/Mono.
        /// </remarks>
        public long MemoryLimitBytes { get; init; }

        /// <summary>
        /// Gets the CLR types exposed to scripts as globals under their <see cref="MemberInfo.Name"/>
        /// (constructible, static members callable, and usable as the <see cref="Type"/> argument of
        /// <c>entity.HasComponent/GetComponent/SetComponent/RemoveComponent</c>).
        /// <c>Entity</c> is always exposed.
        /// </summary>
        public IReadOnlyList<Type> ReferenceTypes { get; init; } = Array.Empty<Type>();

        /// <summary>
        /// Gets assemblies whose namespaces scripts may reach via <c>importNamespace</c>, in addition to DefaultEcs.
        /// </summary>
        public IReadOnlyList<Assembly> ClrAssemblies { get; init; } = Array.Empty<Assembly>();

        /// <summary>
        /// Gets an escape hatch applied last to the Jint <see cref="Options"/> (extra constraints, converters, ...).
        /// </summary>
        public Action<Options>? ConfigureEngine { get; init; }
    }
}
