using System.Collections.Concurrent;
using System.Reflection;
using DefaultEcs;
using deniszykov.TypeConversion;
using Fasterflect;
using Microsoft.Extensions.Options;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// A class used to set global components in the entity (components their instances would be shared between entities)
    /// </summary>
    internal class GlobalComponentInEntitySetter : BaseComponentInEntitySetter
    {
        private static readonly ConcurrentDictionary<Type, MethodInfo> EntitySetSameAsWorldMethodCache = new();
        private static readonly ConcurrentDictionary<Type, MethodInfo> WorldSetMethodCache = new();
        private static readonly ConcurrentDictionary<Type, MethodInfo> WorldHasMethodCache = new();

        private static readonly MethodInfo? EntitySetSameAsWorldMethod =
            typeof(Entity).Methods(nameof(Entity.SetSameAsWorld))
                .FirstOrDefault();

        private static readonly MethodInfo? WorldSetMethod =
            typeof(World).Methods(nameof(DefaultEcs.World.Set))
                .FirstOrDefault(m => m.Parameters().Count == 1);

        private static readonly MethodInfo? WorldHasMethod =
            typeof(World).Methods(nameof(DefaultEcs.World.Has))
                .FirstOrDefault();

        private readonly TypeConversionProvider _typeConversionProvider = new(Options.Create(new TypeConversionProviderOptions
        {
            Options = ConversionOptions.UseDefaultFormatIfNotSpecified,
        }));

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalComponentInEntitySetter"/> class
        /// </summary>
        /// <param name="world">entity "world" to operate on</param>
        /// <exception cref="InvalidOperationException">Failed to detect Entity::Set(ref T param) method, this probably means DefaultEcs was updated and had a breaking change. This is not supposed to happen.</exception>
        public GlobalComponentInEntitySetter(World world)
            : base(world)
        {
            if (EntitySetSameAsWorldMethodCache == null)
            {
                throw new InvalidOperationException(
                    "Failed to detect Entity::SetSameAsWorld<T>() method, this probably means DefaultEcs was updated and had a breaking change. This is not supposed to happen and should be reported");
            }

            if (WorldSetMethod == null)
            {
                throw new InvalidOperationException(
                    "Failed to detect World::Set<T>(ref T param) method, this probably means DefaultEcs was updated and had a breaking change. This is not supposed to happen and should be reported");
            }

            if (WorldHasMethod == null)
            {
                throw new InvalidOperationException(
                    "Failed to detect World::Has<T>() method, this probably means DefaultEcs was updated and had a breaking change. This is not supposed to happen and should be reported");
            }
        }

        /// <inheritdoc/>
        internal override bool CanSetComponent(Type componentType) =>
            componentType.HasAttribute<ComponentAttribute>() &&
            componentType.Attribute<ComponentAttribute>().IsGlobal;

        /// <inheritdoc/>
        internal override void SetComponent(in Entity entity, Type componentType, object componentInstance)
        {
            var genericWorldHasMethod = GetWorldHasMethod(componentType);

            var hasSuchComponent = (bool)genericWorldHasMethod.Call(World);
            if (!hasSuchComponent)
            {
                var genericWorldSetMethod = GetWorldSetMethod(componentType);

                genericWorldSetMethod.Call(
                    World,
                    _typeConversionProvider.Convert(typeof(object), componentType, componentInstance));
            }

            var genericSetSameAsWorldMethod = GetSameAsWorldMethod(componentType);

            genericSetSameAsWorldMethod.Call(entity.WrapIfValueType());
        }

        private static MethodInfo GetSameAsWorldMethod(Type componentType)
        {
            var genericSetSameAsWorldMethod = EntitySetSameAsWorldMethodCache.GetOrAdd(
                componentType,
                type => (EntitySetSameAsWorldMethod ??
                         throw new InvalidOperationException(
                             $"failed to create method delegate ({nameof(EntitySetSameAsWorldMethod)})"))
                    .MakeGenericMethod(type));
            return genericSetSameAsWorldMethod;
        }

        private static MethodInfo GetWorldSetMethod(Type componentType)
        {
            var genericWorldSetMethod = WorldSetMethodCache.GetOrAdd(
                componentType,
                type => (WorldSetMethod ??
                         throw new InvalidOperationException($"failed to create method delegate ({nameof(WorldSetMethod)})"))
                    .MakeGenericMethod(type));

            return genericWorldSetMethod;
        }

        private static MethodInfo GetWorldHasMethod(Type componentType)
        {
            var genericWorldHasMethod =
                WorldHasMethodCache.GetOrAdd(
                    componentType,
                    type => (WorldHasMethod ??
                             throw new InvalidOperationException($"failed to create method delegate ({nameof(WorldHasMethod)}"))
                        .MakeGenericMethod(type));

            return genericWorldHasMethod;
        }
    }
}
