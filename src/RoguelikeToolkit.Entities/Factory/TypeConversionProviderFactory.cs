using deniszykov.TypeConversion;
using Microsoft.Extensions.Options;
using static deniszykov.TypeConversion.ConversionOptions;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// Single factory for <see cref="TypeConversionProvider"/> instances so all spawn paths
    /// share the same base configuration. Component-specific conversions (dice, scripts)
    /// are still registered by <see cref="ComponentFactory"/> on top of the shared base.
    /// </summary>
    internal static class TypeConversionProviderFactory
    {
        // Building a provider re-indexes the conversion table (~100us), so the
        // read-only consumers share one process-wide instance instead of paying
        // it per factory. Providers are never mutated after creation here.
        private static readonly Lazy<TypeConversionProvider> SharedBaseProvider =
            new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Gets the process-wide shared base provider. Read-only use only, never register on it.
        /// </summary>
        internal static TypeConversionProvider SharedBase => SharedBaseProvider.Value;

        /// <summary>
        /// Creates a base-configured <see cref="TypeConversionProvider"/>.
        /// </summary>
        /// <returns>a new provider instance with the shared base configuration.</returns>
        internal static TypeConversionProvider Create() => new(Options.Create(new TypeConversionProviderOptions
        {
            Options = UseDefaultFormatIfNotSpecified,
        }));
    }
}
