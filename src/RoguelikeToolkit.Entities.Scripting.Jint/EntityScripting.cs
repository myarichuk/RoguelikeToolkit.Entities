using System.Collections.Concurrent;

namespace RoguelikeToolkit.Entities.Scripting.Jint
{
    /// <summary>
    /// Wires the script types into template loading: call <see cref="Register"/> once at startup, before spawning
    /// entities whose components have <see cref="Script"/>, <see cref="EntityScript"/>, <see cref="EntityComponentScript"/>
    /// or <see cref="EntityInteractionScript"/> members.
    /// </summary>
    /// <remarks>
    /// Explicit on purpose: no module initializers or static constructors that may or may not have run by the time the
    /// first template spawns (IL2CPP and trimming both make that ordering fragile). Without registration a spawn fails
    /// with an <see cref="InvalidCastException"/> naming the script type, never silently.
    /// </remarks>
    public static class EntityScripting
    {
        private static readonly ConcurrentDictionary<string, Script> Scripts = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, EntityScript> EntityScripts = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, EntityComponentScript> ComponentScripts = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, EntityInteractionScript> InteractionScripts = new(StringComparer.Ordinal);

        /// <summary>
        /// Registers the script template parsers with <see cref="TemplateValueParsers"/>. Idempotent and thread-safe.
        /// </summary>
        /// <remarks>
        /// Scripts are immutable, so every template value with the same source text shares one instance (and the parsed
        /// program is shared by all script types, see <see cref="Script"/>): spawning N entities parses once.
        /// </remarks>
        public static void Register()
        {
            TemplateValueParsers.Register(static source => Scripts.GetOrAdd(source, static s => new Script(s)));
            TemplateValueParsers.Register(static source => EntityScripts.GetOrAdd(source, static s => new EntityScript(s)));
            TemplateValueParsers.Register(static source => ComponentScripts.GetOrAdd(source, static s => new EntityComponentScript(s)));
            TemplateValueParsers.Register(static source => InteractionScripts.GetOrAdd(source, static s => new EntityInteractionScript(s)));
        }
    }
}
