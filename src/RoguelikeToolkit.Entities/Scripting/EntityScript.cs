using System.Runtime.CompilerServices;
using DefaultEcs;

namespace RoguelikeToolkit.Entities.Scripting
{
    /// <summary>
    /// A script that runs against an entity, bound to the global <c>entity</c>.
    /// </summary>
    public sealed class EntityScript
    {
        /// <summary>
        /// Global name of the entity.
        /// </summary>
        public const string InstanceName = "entity";

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityScript"/> class.
        /// </summary>
        /// <param name="source">JavaScript source</param>
        public EntityScript(string source) => Script = new Script(source);

        /// <summary>
        /// Gets the underlying script.
        /// </summary>
        public Script Script { get; }

        /// <summary>
        /// Gets the JavaScript source.
        /// </summary>
        public string Source => Script.Source;

        /// <summary>
        /// Runs the script on <paramref name="entity"/>.
        /// </summary>
        /// <param name="host">engine to run on</param>
        /// <param name="entity">entity bound to <c>entity</c></param>
        /// <param name="arguments">extra globals for this execution</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteOn(ScriptHost host, ref Entity entity, params ScriptArgument[] arguments) =>
            Script.ExecuteOn(host, ref entity, InstanceName, arguments);

        /// <inheritdoc />
        public override string ToString() => Source;
    }
}
