using System.Collections.Concurrent;
using System.Reflection;
using DefaultEcs;
using Fasterflect;
using deniszykov.TypeConversion;
using Microsoft.Extensions.Options;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// A class used to set "regular", non-global components in the entity
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    internal class RegularComponentInEntitySetter : BaseComponentInEntitySetter
    {
        private static readonly ConcurrentDictionary<Type, MethodInfo> EntitySetMethodCache = new();

        private static readonly MethodInfo? EntitySetMethod =
            typeof(Entity).Methods(nameof(Entity.Set))
                .FirstOrDefault(m => m.Parameters().Count == 1);

        private readonly TypeConversionProvider _typeConversionProvider = new(Options.Create(new TypeConversionProviderOptions
        {
            Options = ConversionOptions.UseDefaultFormatIfNotSpecified,
        }));

        /// <summary>
        /// Initializes a new instance of the <see cref="RegularComponentInEntitySetter"/> class
        /// </summary>
        /// <param name="world">entity "world" to operate on</param>
        /// <exception cref="InvalidOperationException">Failed to detect Entity::Set(ref T param) method, this probably means DefaultEcs was updated and had a breaking change. This is not supposed to happen.</exception>
        public RegularComponentInEntitySetter(World world)
            : base(world)
        {
            // sanity check
            if (EntitySetMethod == null)
            {
                throw new InvalidOperationException(
                    "Failed to detect Entity::Set<T>(ref T param) method, this probably means DefaultEcs was updated and had a breaking change. This is not supposed to happen and should be reported");
            }
        }

        /// <inheritdoc />
        internal override bool CanSetComponent(Type componentType) =>
            !componentType.HasAttribute<ComponentAttribute>() ||
            !componentType.Attribute<ComponentAttribute>().IsGlobal;

        /// <inheritdoc />
        internal override void SetComponent(in Entity entity, Type componentType, object componentInstance)
        {
            var genericEntitySetMethod =
                EntitySetMethodCache.GetOrAdd(
                    componentType,
                    type => (EntitySetMethod ??
                             throw new InvalidOperationException(
                                 $"failed to create method delegate ({nameof(EntitySetMethod)}"))
                        .MakeGenericMethod(type));

            genericEntitySetMethod.Call(
                entity.WrapIfValueType(),
                _typeConversionProvider.Convert(typeof(object), componentType, componentInstance));
        }
    }
}
