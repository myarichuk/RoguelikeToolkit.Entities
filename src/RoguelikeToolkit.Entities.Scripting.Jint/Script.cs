using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Jint;
using JsScript = Acornima.Ast.Script;

namespace RoguelikeToolkit.Entities.Scripting.Jint
{
    /// <summary>
    /// An immutable, pre-parsed JavaScript snippet that runs against one or two CLR values on a <see cref="ScriptHost"/>.
    /// </summary>
    /// <remarks>
    /// Parsing happens once per distinct source text per process (the prepared AST is cached and shared by every
    /// <see cref="Script"/> with the same source, across hosts and threads). A script holds no execution state, so one
    /// instance can be shared by any number of entities; the entity factory does exactly that for template scripts.
    /// </remarks>
    public sealed class Script
    {
        /// <summary>
        /// Default global name of the target in single-target executions.
        /// </summary>
        public const string SingleTargetInstanceName = "target";

        /// <summary>
        /// Default global name of the source in two-target executions.
        /// </summary>
        public const string TwoTargetSourceInstanceName = "source";

        /// <summary>
        /// Default global name of the target in two-target executions.
        /// </summary>
        public const string TwoTargetTargetInstanceName = "target";

        private static readonly ConcurrentDictionary<string, Prepared<JsScript>> PreparedCache = new(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a new instance of the <see cref="Script"/> class.
        /// </summary>
        /// <param name="source">JavaScript source</param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null</exception>
        /// <exception cref="Acornima.ParseErrorException"><paramref name="source"/> is not valid JavaScript</exception>
        public Script(string source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Prepared = PreparedCache.GetOrAdd(source, static s => Prepare(s));
        }

        /// <summary>
        /// Gets the JavaScript source, as written.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets the parsed program (shared, immutable).
        /// </summary>
        internal Prepared<JsScript> Prepared { get; }

        /// <summary>
        /// Runs the script with <paramref name="target"/> bound to <c>target</c>.
        /// </summary>
        /// <typeparam name="TTarget">target type; value types are written back after execution</typeparam>
        /// <param name="host">engine to run on</param>
        /// <param name="target">value the script reads/mutates</param>
        /// <param name="arguments">extra globals for this execution</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteOn<TTarget>(ScriptHost host, ref TTarget target, params ScriptArgument[] arguments) =>
            ExecuteOn(host, ref target, SingleTargetInstanceName, arguments);

        /// <summary>
        /// Runs the script with <paramref name="target"/> bound to <paramref name="instanceName"/>.
        /// </summary>
        /// <typeparam name="TTarget">target type; value types are written back after execution</typeparam>
        /// <param name="host">engine to run on</param>
        /// <param name="target">value the script reads/mutates</param>
        /// <param name="instanceName">global name of the target</param>
        /// <param name="arguments">extra globals for this execution</param>
        /// <exception cref="ArgumentNullException"><paramref name="host"/> is null</exception>
        public void ExecuteOn<TTarget>(ScriptHost host, ref TTarget target, string instanceName, params ScriptArgument[] arguments)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            host.Execute(this, ref target, instanceName, arguments ?? Array.Empty<ScriptArgument>());
        }

        /// <summary>
        /// Runs the script with <paramref name="source"/> bound to <c>source</c> and <paramref name="target"/> to <c>target</c>.
        /// </summary>
        /// <typeparam name="TSource">source type</typeparam>
        /// <typeparam name="TTarget">target type</typeparam>
        /// <param name="host">engine to run on</param>
        /// <param name="source">first value</param>
        /// <param name="target">second value</param>
        /// <param name="arguments">extra globals for this execution</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteOn<TSource, TTarget>(ScriptHost host, ref TSource source, ref TTarget target, params ScriptArgument[] arguments) =>
            ExecuteOn(host, ref source, TwoTargetSourceInstanceName, ref target, TwoTargetTargetInstanceName, arguments);

        /// <summary>
        /// Runs the script with two named targets.
        /// </summary>
        /// <typeparam name="TSource">source type</typeparam>
        /// <typeparam name="TTarget">target type</typeparam>
        /// <param name="host">engine to run on</param>
        /// <param name="source">first value</param>
        /// <param name="sourceInstanceName">global name of the first value</param>
        /// <param name="target">second value</param>
        /// <param name="targetInstanceName">global name of the second value</param>
        /// <param name="arguments">extra globals for this execution</param>
        /// <exception cref="ArgumentNullException"><paramref name="host"/> is null</exception>
        public void ExecuteOn<TSource, TTarget>(
            ScriptHost host,
            ref TSource source,
            string sourceInstanceName,
            ref TTarget target,
            string targetInstanceName,
            params ScriptArgument[] arguments)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            host.Execute(this, ref source, sourceInstanceName, ref target, targetInstanceName, arguments ?? Array.Empty<ScriptArgument>());
        }

        /// <inheritdoc />
        public override string ToString() => Source;

        // Function scope per execution: declarations stay local to the run, `return` exits early. The wrapper opens on
        // the source's first line so reported line numbers match the template; the newline guards a trailing // comment.
        private static Prepared<JsScript> Prepare(string source) =>
            Engine.PrepareScript("(function(){" + source + "\n})();");
    }
}
