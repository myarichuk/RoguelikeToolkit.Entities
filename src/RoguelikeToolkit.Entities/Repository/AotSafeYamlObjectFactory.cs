using YamlDotNet.Serialization.ObjectFactories;

namespace RoguelikeToolkit.Entities.Repository
{
    /// <summary>
    /// YamlDotNet object factory that constructs the container types templates deserialize into with plain
    /// <c>new</c> instead of <see cref="Activator"/>. The constructors are statically referenced, so AOT linkers
    /// (Unity's UnityLinker under IL2CPP, NativeAOT's trimmer) cannot strip them; without this, loading a template
    /// fails with "No parameterless constructor defined" once stripping removes the reflection-only constructor.
    /// Every other type falls back to the default (reflection-based) factory.
    /// </summary>
    internal sealed class AotSafeYamlObjectFactory : DefaultObjectFactory
    {
        /// <inheritdoc />
        public override object Create(Type type)
        {
            if (type == typeof(Dictionary<string, object>))
            {
                return new Dictionary<string, object>();
            }

            if (type == typeof(Dictionary<object, object>))
            {
                return new Dictionary<object, object>();
            }

            if (type == typeof(List<object>))
            {
                return new List<object>();
            }

            if (type == typeof(List<string>))
            {
                return new List<string>();
            }

            return base.Create(type);
        }
    }
}
