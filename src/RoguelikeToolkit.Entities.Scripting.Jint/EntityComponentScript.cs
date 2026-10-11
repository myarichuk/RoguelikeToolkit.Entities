using DefaultEcs;

namespace RoguelikeToolkit.Entities.Scripting.Jint
{
    /// <summary>
    /// A script that runs against entity components: one component bound to <c>component</c>, or a pair bound to
    /// <c>source</c> / <c>target</c>. Changed components are written back with <c>Entity.Set</c>, so DefaultEcs
    /// change notifications fire.
    /// </summary>
    public sealed class EntityComponentScript
    {
        /// <summary>
        /// Global name of the component in single-component executions.
        /// </summary>
        public const string InstanceName = "component";

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityComponentScript"/> class.
        /// </summary>
        /// <param name="source">JavaScript source</param>
        public EntityComponentScript(string source) => Script = new Script(source);

        /// <summary>
        /// Gets the underlying script.
        /// </summary>
        public Script Script { get; }

        /// <summary>
        /// Gets the JavaScript source.
        /// </summary>
        public string Source => Script.Source;

        /// <summary>
        /// Runs the script on the entity's <typeparamref name="TComponent"/>.
        /// </summary>
        /// <typeparam name="TComponent">component type</typeparam>
        /// <param name="host">engine to run on</param>
        /// <param name="entity">entity that owns the component</param>
        /// <param name="arguments">extra globals for this execution</param>
        /// <returns>false if the entity does not have the component (the script did not run)</returns>
        public bool TryExecuteOn<TComponent>(ScriptHost host, in Entity entity, params ScriptArgument[] arguments)
        {
            if (!entity.Has<TComponent>())
            {
                return false;
            }

            var component = entity.Get<TComponent>();
            Script.ExecuteOn(host, ref component, InstanceName, arguments);
            entity.Set(component);
            return true;
        }

        /// <summary>
        /// Runs the script on a component pair of two entities.
        /// </summary>
        /// <typeparam name="TSourceComponent">component type of <paramref name="source"/></typeparam>
        /// <typeparam name="TTargetComponent">component type of <paramref name="target"/></typeparam>
        /// <param name="host">engine to run on</param>
        /// <param name="source">entity whose component is bound to <c>source</c></param>
        /// <param name="target">entity whose component is bound to <c>target</c></param>
        /// <param name="arguments">extra globals for this execution</param>
        /// <returns>false if either entity lacks its component (the script did not run)</returns>
        public bool TryExecuteOn<TSourceComponent, TTargetComponent>(
            ScriptHost host,
            in Entity source,
            in Entity target,
            params ScriptArgument[] arguments)
        {
            if (!source.Has<TSourceComponent>() || !target.Has<TTargetComponent>())
            {
                return false;
            }

            var sourceComponent = source.Get<TSourceComponent>();
            var targetComponent = target.Get<TTargetComponent>();
            Script.ExecuteOn(host, ref sourceComponent, ref targetComponent, arguments);
            source.Set(sourceComponent);
            target.Set(targetComponent);
            return true;
        }

        /// <inheritdoc />
        public override string ToString() => Source;
    }
}
