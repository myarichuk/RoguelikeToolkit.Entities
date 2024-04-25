using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using deniszykov.TypeConversion;
using Fasterflect;
using Microsoft.Extensions.Options;
using ObjectTreeWalker;
using RoguelikeToolkit.DiceExpression;
using RoguelikeToolkit.Entities.Components;
using RoguelikeToolkit.Entities.Extensions;
using RoguelikeToolkit.Scripts;
using static deniszykov.TypeConversion.ConversionOptions;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// Factory helper that creates a component instance by type and sets it's values using provided data
    /// </summary>
    internal class ComponentFactory
    {
        private readonly ConcurrentDictionary<Type, IList<PropertyInfo>> _typePropertyCache = new();
        private readonly ObjectMemberIterator _memberIterator = new();

        private readonly TypeConversionProvider _typeConversionProvider = new(Options.Create(new TypeConversionProviderOptions
        {
            Options = UseDefaultFormatIfNotSpecified,
        }));

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentFactory"/> class
        /// </summary>
        public ComponentFactory()
        {
            _typeConversionProvider.RegisterConversion<string, Dice>(
                (src, _, __) =>
                    Dice.Parse(src, true),
                ConversionQuality.Custom);

            _typeConversionProvider.RegisterConversion<string, EntityScript>(
                (src, _, __) =>
                    new EntityScript(src),
                ConversionQuality.Custom);

            _typeConversionProvider.RegisterConversion<string, EntityComponentScript>(
                (src, _, __) =>
                    new EntityComponentScript(src),
                ConversionQuality.Custom);

            _typeConversionProvider.RegisterConversion<string, EntityInteractionScript>(
                (src, _, __) =>
                    new EntityInteractionScript(src),
                ConversionQuality.Custom);

            _typeConversionProvider.RegisterConversion<string, Script>(
                (src, _, __) =>
                    new Script(src),
                ConversionQuality.Custom);
        }

        /// <summary>
        /// Try and create an instance of specified type from the data provided by the dictionary.
        /// Needed for initializing components deserialized from templates.
        /// </summary>
        /// <param name="componentType">Component type to create</param>
        /// <param name="objectData">Property data, typically received from YamlDotNet deserialization</param>
        /// <param name="instance">resulting instance of the component</param>
        /// <returns>true if instance creation succeeded, false otherwise</returns>
        /// <remarks>This overload is intended for value-type components</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="objectData"/> is <see langword="null"/></exception>
        public bool TryCreateValueInstance(Type componentType, object objectData, out object? instance)
        {
            ValidateValueComponentInputThrowIfNeeded(componentType, objectData);

            instance = default;
            var instanceAsObject = CreateEmptyInstance(componentType);

            // ReSharper disable once ExceptionNotDocumented
            var valueComponentType = componentType.GetInterfaces()
                .FirstOrDefault(i => i.FullName?.Contains(nameof(IValueComponent<object>)) ?? false);

            instanceAsObject.SetPropertyValue(
                nameof(IValueComponent<object>.Value),
                _typeConversionProvider.Convert(
                    objectData.GetType(),
                    valueComponentType?.GenericTypeArguments[0] ?? throw new InvalidOperationException("This is not supposed to happen and is likely a bug."),
                    objectData));

            instance = instanceAsObject.UnwrapIfWrapped();
            return true;
        }

        /// <summary>
        /// Try and create an instance of specified type from the data provided by the dictionary.
        /// Needed for initializing components deserialized from templates.
        /// </summary>
        /// <param name="componentType">Component type to create</param>
        /// <param name="objectData">Property data, typically received from YamlDotNet deserialization</param>
        /// <param name="instance">resulting instance of the component</param>
        /// <returns>true if instance creation succeeded, false otherwise</returns>
        /// <remarks>This overload is intended for object components with properties</remarks>
        public bool TryCreateReferenceInstance(Type componentType, IReadOnlyDictionary<object, object> objectData, out object? instance)
        {
            ValidateNonValueComponentInputThrowIfNeeded(componentType, objectData);
            instance = default;
            var instanceAsObject = CreateEmptyInstance(componentType);

            _memberIterator.Traverse(
                instanceAsObject,
                (in MemberAccessor accessor) =>
                {
                    var memberName = accessor.Name;

                    var relevantPair = GetRelevantPair(accessor.PropertyPath.Select(x => x.Name), memberName, objectData);

                    if (relevantPair.Value != null)
                    {
                        var convertedValue = ConvertValueFromSrcToDestType(
                            relevantPair.Value,
                            accessor.Type);
                        if (convertedValue != null)
                        {
                            accessor.SetValue(convertedValue);
                        }
                    }
                },
                (in MemberAccessor accessor) => accessor.MemberType == MemberType.Property);

            instance = instanceAsObject.UnwrapIfWrapped();
            return true;

            KeyValuePair<object, object> GetRelevantPair(
                IEnumerable<string> propertyPath,
                string memberName,
                IReadOnlyDictionary<object, object> objectData)
            {
                if (!propertyPath.Any())
                {
                    return objectData.FirstOrDefault(kvp =>
                        string.Equals((string)kvp.Key, memberName, StringComparison.InvariantCultureIgnoreCase));
                }

                var currentObjectData = objectData;

                foreach (var pathElement in propertyPath)
                {
                    var subElement = currentObjectData!.FirstOrDefault(x =>
                        string.Equals(x.Key as string, pathElement, StringComparison.InvariantCultureIgnoreCase));

                    if (subElement.Value is IReadOnlyDictionary<object, object> subObject)
                    {
                        if (propertyPath.Last() == pathElement)
                        {
                            return subElement;
                        }

                        currentObjectData = subObject;
                    }
                    else
                    {
                        return subElement;
                    }
                }

                return currentObjectData.FirstOrDefault(kvp =>
                    string.Equals((string)kvp.Key, memberName, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        /// <summary>
        /// Try and create an instance of specified type from the data provided by the dictionary.
        /// Needed for initializing components deserialized from templates.
        /// </summary>
        /// <typeparam name="TComponent">Type of the object to populate with the data</typeparam>
        /// <param name="objectData">Property data, typically received from YamlDotNet deserialization</param>
        /// <param name="instance">resulting instance of the component</param>
        /// <returns>true if instance creation succeeded, false otherwise</returns>
        public bool TryCreateReferenceInstance<TComponent>(IReadOnlyDictionary<object, object> objectData, out TComponent instance)
        {
            var success = TryCreateReferenceInstance(typeof(TComponent), objectData, out var instanceAsObject);
            instance = (TComponent)instanceAsObject!;
            return success;
        }

        private static void ValidateValueComponentInputThrowIfNeeded(Type componentType, object objectData)
        {
            if (!componentType.IsValueComponentType())
            {
                throw new ArgumentException($"The type doesn't implement IValueComponent<T>", nameof(componentType));
            }

            if (objectData == null)
            {
                throw new ArgumentNullException(nameof(objectData));
            }
        }

        private static void ValidateNonValueComponentInputThrowIfNeeded(Type componentType, IReadOnlyDictionary<object, object> objectData)
        {
            if (objectData == null)
            {
                throw new ArgumentNullException(nameof(objectData));
            }

            if (componentType.IsValueComponentType())
            {
                throw new ArgumentException(
                    $"The type implements IValueComponent<T>, use the other overload for correct functionality",
                    nameof(componentType));
            }
        }

        private static object CreateEmptyInstance(Type type) =>
            RuntimeHelpers.GetUninitializedObject(type).WrapIfValueType();

        private bool TrySetPropertyValue(Type componentType, KeyValuePair<object, object> propertyData, object instanceAsObject)
        {
            if (!TryGetDestPropertyFor(componentType, propertyData.Key, out var property))
            {
                return true;
            }

            // note: ConvertValueFromSrcToDestType() can call recursively to this method (TryCreateValueInstance)
            if (!instanceAsObject.TrySetPropertyValue(
                    property!.Name,
                    ConvertValueFromSrcToDestType(
                        propertyData.Value,
                        property.PropertyType)))
            {
                // TODO: add logging for the failure
                return false;
            }

            return true;
        }

        private object? ConvertValueFromSrcToDestType(object srcValue, Type destType)
        {
            object? convertResult;
            switch (srcValue)
            {
                case Dictionary<object, object> valueAsDictionary:
                    {
                        TryCreateReferenceInstance(destType, valueAsDictionary, out convertResult);
                        break;
                    }

                // primitive or string!
                default:
                    convertResult =
                        _typeConversionProvider.Convert(srcValue.GetType(), destType, srcValue);
                    break;
            }

            return convertResult;
        }

        private bool TryGetDestPropertyFor(Type componentType, object destPropertyKey, out PropertyInfo? property)
        {
            // note: since ware deserializing yaml, propertyData.Key will always be string, this is precaution
            var properties = _typePropertyCache.GetOrAdd(
                componentType,
                type => type.PropertiesWith(Flags.InstancePublic));

            property = properties.FirstOrDefault(p => p.Name.Equals(destPropertyKey as string, StringComparison.InvariantCultureIgnoreCase));
            return property != null;
        }
    }
}
