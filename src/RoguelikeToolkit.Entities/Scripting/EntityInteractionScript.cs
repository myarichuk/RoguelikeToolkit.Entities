using System.Runtime.CompilerServices;
using DefaultEcs;

namespace RoguelikeToolkit.Entities.Scripting
{
    /// <summary>
    /// A script that runs against two entities, bound to the globals <c>source</c> and <c>target</c>.
    /// </summary>
    public sealed class EntityInteractionScript
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntityInteractionScript"/> class.
        /// </summary>
        /// <param name="source">JavaScript source</param>
        public EntityInteractionScript(string source) => Script = new Script(source);

        /// <summary>
        /// Gets the underlying script.
        /// </summary>
        public Script Script { get; }

        /// <summary>
        /// Gets the JavaScript source.
        /// </summary>
        public string Source => Script.Source;

        /// <summary>
        /// Runs the script on an interacting pair of entities.
        /// </summary>
        /// <param name="host">engine to run on</param>
        /// <param name="source">entity bound to <c>source</c></param>
        /// <param name="target">entity bound to <c>target</c></param>
        /// <param name="arguments">extra globals for this execution</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteOn(ScriptHost host, ref Entity source, ref Entity target, params ScriptArgument[] arguments) =>
            Script.ExecuteOn(host, ref source, ref target, arguments);

        /// <inheritdoc />
        public override string ToString() => Source;
    }
}
