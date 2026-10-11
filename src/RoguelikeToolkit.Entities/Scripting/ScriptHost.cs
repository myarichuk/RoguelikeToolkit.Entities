using System.Diagnostics.CodeAnalysis;
using DefaultEcs;
using Jint;
using Jint.Native;
using Jint.Runtime.Interop;

namespace RoguelikeToolkit.Entities.Scripting
{
    /// <summary>
    /// One Jint engine that executes any number of scripts. Scripts themselves are immutable, pre-parsed and shared;
    /// the host is where execution state lives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not thread-safe (neither is a Jint engine): own one host per thread / simulation loop and reuse it. Creating an
    /// engine costs far more than running a typical component script, which is why <c>RoguelikeToolkit.Scripts</c>'
    /// engine-per-script(-per-entity) model is gone.
    /// </para>
    /// <para>
    /// Every script runs wrapped in its own function scope, so <c>var</c>/<c>let</c>/function declarations do not leak
    /// between scripts sharing a host and <c>return</c> exits early. Bindings (<c>entity</c>, <c>component</c>,
    /// arguments, ...) are cleared after each execution. Assigning to an undeclared name still creates a global, as in
    /// any sloppy-mode JavaScript; keep state in components, not in the engine.
    /// </para>
    /// </remarks>
    public sealed class ScriptHost : IDisposable
    {
        private readonly Engine _engine;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptHost"/> class with default limits.
        /// </summary>
        public ScriptHost()
            : this(new ScriptHostOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptHost"/> class.
        /// </summary>
        /// <param name="options">limits and interop surface</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null</exception>
        // Jint discovers the interop methods through reflection only: keep them under trimming (ILLink/NativeAOT).
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(EntityScriptExtensions))]
        public ScriptHost(ScriptHostOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _engine = new Engine(o =>
            {
                if (options.RecursionLimit > 0)
                {
                    o.LimitRecursion(options.RecursionLimit);
                }

                if (options.MaxStatements > 0)
                {
                    o.MaxStatements(options.MaxStatements);
                }

                if (options.Timeout is { } timeout)
                {
                    o.TimeoutInterval(timeout);
                }

                if (options.MemoryLimitBytes > 0)
                {
                    o.LimitMemory(options.MemoryLimitBytes);
                }

                o.Interop.ExtensionMethodTypes.Add(typeof(EntityScriptExtensions));
                o.AllowClr(options.ClrAssemblies.Prepend(typeof(Entity).Assembly).Distinct().ToArray());
                options.ConfigureEngine?.Invoke(o);
            });

            _engine.SetValue(nameof(Entity), TypeReference.CreateTypeReference(_engine, typeof(Entity)));
            foreach (var type in options.ReferenceTypes)
            {
                _engine.SetValue(type.Name, TypeReference.CreateTypeReference(_engine, type));
            }
        }

        /// <summary>
        /// Gets the underlying engine, for anything this API does not cover (custom globals, modules, debugging).
        /// </summary>
        public Engine Engine => _engine;

        /// <inheritdoc />
        public void Dispose() => _engine.Dispose();

        /// <summary>
        /// Runs <paramref name="script"/> with <paramref name="target"/> bound to <paramref name="targetName"/>, then
        /// reads the binding back (value types are mutated through their box; scripts may also reassign the binding).
        /// </summary>
        /// <typeparam name="TTarget">target type</typeparam>
        /// <param name="script">prepared script</param>
        /// <param name="target">bound value, replaced by the value after execution</param>
        /// <param name="targetName">global name of the target</param>
        /// <param name="arguments">extra globals for this execution</param>
        internal void Execute<TTarget>(Script script, ref TTarget target, string targetName, ScriptArgument[] arguments)
        {
            Bind(arguments);
            var boundTarget = Bind(targetName, target);
            try
            {
                _engine.Execute(script.Prepared);
                target = ReadBack<TTarget>(targetName, boundTarget);
            }
            finally
            {
                Unbind(targetName, arguments);
            }
        }

        /// <summary>
        /// Two-target variant of <see cref="Execute{TTarget}"/>.
        /// </summary>
        /// <typeparam name="TSource">source type</typeparam>
        /// <typeparam name="TTarget">target type</typeparam>
        /// <param name="script">prepared script</param>
        /// <param name="source">first bound value, replaced by its value after execution</param>
        /// <param name="sourceName">global name of the first value</param>
        /// <param name="target">second bound value, replaced by its value after execution</param>
        /// <param name="targetName">global name of the second value</param>
        /// <param name="arguments">extra globals for this execution</param>
        internal void Execute<TSource, TTarget>(
            Script script,
            ref TSource source,
            string sourceName,
            ref TTarget target,
            string targetName,
            ScriptArgument[] arguments)
        {
            Bind(arguments);
            var boundTarget = Bind(targetName, target);
            var boundSource = Bind(sourceName, source);
            try
            {
                _engine.Execute(script.Prepared);
                target = ReadBack<TTarget>(targetName, boundTarget);
                source = ReadBack<TSource>(sourceName, boundSource);
            }
            finally
            {
                Unbind(targetName, arguments);
                _engine.SetValue(sourceName, JsValue.Undefined);
            }
        }

        private void Bind(ScriptArgument[] arguments)
        {
            foreach (var argument in arguments)
            {
                _engine.SetValue(argument.Name, JsValue.FromObject(_engine, argument.Value));
            }
        }

        private JsValue Bind<T>(string name, T value)
        {
            // boxes value types once; the wrapper writes members into that box, which ReadBack unboxes
            var jsValue = JsValue.FromObject(_engine, value);
            _engine.SetValue(name, jsValue);
            return jsValue;
        }

        private T ReadBack<T>(string name, JsValue bound)
        {
            var current = _engine.GetValue(name);
            if (ReferenceEquals(current, bound) && current is IObjectWrapper wrapper)
            {
                // fast path: binding untouched (members may have been written into the wrapped instance)
                return (T)wrapper.Target;
            }

            return current.ToObject() is T converted
                ? converted
                : throw new InvalidCastException(
                    $"Script reassigned '{name}' to a value that is not a {typeof(T).FullName} ({current.Type}).");
        }

        private void Unbind(string targetName, ScriptArgument[] arguments)
        {
            _engine.SetValue(targetName, JsValue.Undefined);
            foreach (var argument in arguments)
            {
                _engine.SetValue(argument.Name, JsValue.Undefined);
            }
        }
    }
}
