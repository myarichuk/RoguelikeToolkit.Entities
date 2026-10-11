using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using deniszykov.TypeConversion;
using Microsoft.Extensions.Options;
using RoguelikeToolkit.DiceExpression;
using RoguelikeToolkit.Entities.Components;
using RoguelikeToolkit.Entities.Extensions;
using RoguelikeToolkit.Scripts;
using static deniszykov.TypeConversion.ConversionOptions;

namespace RoguelikeToolkit.Entities.Factory;

/// <summary>
/// Factory helper that creates a component instance by type and sets it's values using provided data.
/// Template keys are indexed once per call (case-insensitive), so member lookup is O(1) per member
/// instead of scanning the template data per member. Template properties that match no member are
/// reported via <see cref="EntityDiagnostics"/> (likely typos); everything else behaves as before.
/// </summary>
internal class ComponentFactory
{
    // IValueComponent{T} payload types are per-type constants; calling GetInterfaces() per instance was pure overhead.
    private static readonly ConcurrentDictionary<Type, Type> ValueComponentPayloadCache = new();

    // Dice expressions are immutable once parsed, so identical strings share a single parse.
    // Scripts are intentionally NOT shared: script instances hold per-entity state.
    private static readonly ConcurrentDictionary<string, Dice> DiceParseCache = new();

    // String-parsed component member types. Consulted by the AOT-safe fast path first; the same parsers are also
    // registered on the general provider below so its behavior is unchanged for anything that falls through.
    private static readonly IReadOnlyDictionary<Type, Func<string, object>> StringConverters =
        new Dictionary<Type, Func<string, object>>
        {
            [typeof(Dice)] = static src => DiceParseCache.GetOrAdd(src, static s => Dice.Parse(s, true)),
            [typeof(EntityScript)] = static src => new EntityScript(src),
            [typeof(EntityComponentScript)] = static src => new EntityComponentScript(src),
            [typeof(EntityInteractionScript)] = static src => new EntityInteractionScript(src),
            [typeof(Script)] = static src => new Script(src),
        };

    // Dice/script conversions are stateless registrations over process-wide caches,
    // so all factories share one provider built once instead of per factory.
    private static readonly Lazy<TypeConversionProvider> SharedProvider = new(
        () =>
        {
            var provider = TypeConversionProviderFactory.Create();
            provider.RegisterConversion<string, Dice>(
                (src, _, __) =>
                    DiceParseCache.GetOrAdd(src, static s => Dice.Parse(s, true)),
                ConversionQuality.Custom);

            provider.RegisterConversion<string, EntityScript>(
                (src, _, __) =>
                    new EntityScript(src),
                ConversionQuality.Custom);

            provider.RegisterConversion<string, EntityComponentScript>(
                (src, _, __) =>
                    new EntityComponentScript(src),
                ConversionQuality.Custom);

            provider.RegisterConversion<string, EntityInteractionScript>(
                (src, _, __) =>
                    new EntityInteractionScript(src),
                ConversionQuality.Custom);

            provider.RegisterConversion<string, Script>(
                (src, _, __) =>
                    new Script(src),
                ConversionQuality.Custom);

            return provider;
        },
        LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly TypeConversionProvider _typeConversionProvider = SharedProvider.Value;

    /// <summary>
    /// Try and create an instance of specified type from the data provided by the dictionary.
    /// Needed for initializing components deserialized from templates.
    /// </summary>
    /// <param name="componentType">Component type to create</param>
    /// <param name="objectData">Property data, typically received from YamlDotNet deserialization</param>
    /// <param name="instance">resulting instance of the component</param>
    /// <returns>true if instance creation succeeded, false otherwise</returns>
    /// <remarks>This overload is intended for value-type components.
    /// Failures throw (invalid input, conversion errors) rather than returning false.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="objectData"/> is <see langword="null"/></exception>
    public bool TryCreateValueInstance(Type componentType, object objectData, out object? instance)
    {
        ValidateValueComponentInputThrowIfNeeded(componentType, objectData);

        var instanceAsObject = CreateEmptyInstance(componentType);

        if (!ComponentMembers.For(componentType).TryGet(nameof(IValueComponent<object>.Value), out var valueMember))
        {
            throw new InvalidOperationException(
                $"Value component '{componentType.FullName}' has no settable '{nameof(IValueComponent<object>.Value)}' property.");
        }

        // ReSharper disable once ExceptionNotDocumented
        valueMember.SetValue(
            instanceAsObject,
            ConvertScalar(objectData, GetValueComponentPayloadType(componentType)));

        instance = instanceAsObject;
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
    /// <remarks>This overload is intended for object components with properties.
    /// Failures throw (invalid input, conversion errors) rather than returning false.</remarks>
    public bool TryCreateReferenceInstance(Type componentType, IReadOnlyDictionary<object, object> objectData, out object? instance)
    {
        ValidateNonValueComponentInputThrowIfNeeded(componentType, objectData);
        var instanceAsObject = CreateEmptyInstance(componentType);
        var members = ComponentMembers.For(componentType);

        // Index once (case-insensitive, duplicates reported), then one O(1) member lookup per template key.
        // Nested mappings recurse through ConvertValueFromSrcToDestType, so each level validates its own keys.
        foreach (var kvp in BuildKeyIndex(objectData, componentType))
        {
            if (!members.TryGet(kvp.Key, out var member))
            {
                // typo, removed property, or a field instead of a property
                EntityDiagnostics.Warn(
                    $"Component '{componentType.FullName}' has no member for template property '{kvp.Key}'; the value was ignored. Check whether the template schema is correct.");
                continue;
            }

            if (kvp.Value.Value == null)
            {
                // explicit empty value in the template: leave the member at its default (unchanged behavior)
                continue;
            }

            var convertedValue = ConvertValueFromSrcToDestType(kvp.Value.Value, member.Type);
            if (convertedValue != null)
            {
                member.SetValue(instanceAsObject, convertedValue);
            }
            else
            {
                EntityDiagnostics.Warn(
                    $"Component '{componentType.FullName}' member '{kvp.Key}' resolved a value that converted to null; the value was skipped. Check whether the template schema is correct.");
            }
        }

        // value types: the box was mutated in place, so returning it hands back the populated copy
        instance = instanceAsObject;
        return true;
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

    private static Type GetValueComponentPayloadType(Type componentType) =>
        ValueComponentPayloadCache.GetOrAdd(
            componentType,
            static type => type.GetValueComponentInterface()?.GenericTypeArguments[0]
            ?? throw new InvalidOperationException("This is not supposed to happen and is likely a bug."));

    private static Dictionary<string, KeyValuePair<object, object>> BuildKeyIndex(
        IReadOnlyDictionary<object, object> objectData, Type componentType)
    {
        var index = new Dictionary<string, KeyValuePair<object, object>>(objectData.Count, StringComparer.InvariantCultureIgnoreCase);
        foreach (var kvp in objectData)
        {
            // Throws InvalidOperationException on non-string keys, same as the old per-member scan did.
            var propertyKey = GetPropertyKeyOrThrow(kvp.Key, componentType, "<template data>");
            if (!index.TryAdd(propertyKey, kvp))
            {
                EntityDiagnostics.Warn(
                    $"Component '{componentType.FullName}' has a duplicate template property '{propertyKey}' (keys differ only by case); the later value was ignored. Check whether the template schema is correct.");
            }
        }

        return index;
    }

    private static void ValidateValueComponentInputThrowIfNeeded(Type componentType, object objectData)
    {
        if (!componentType.IsValueComponentType())
        {
            throw new ArgumentException($"The type doesn't implement IValueComponent<T>", nameof(componentType));
        }

        ArgumentGuard.ThrowIfNull(objectData, nameof(objectData));
    }

    private static void ValidateNonValueComponentInputThrowIfNeeded(Type componentType, IReadOnlyDictionary<object, object> objectData)
    {
        ArgumentGuard.ThrowIfNull(objectData, nameof(objectData));

        if (componentType.IsValueComponentType())
        {
            throw new ArgumentException(
                $"The type implements IValueComponent<T>, use the other overload for correct functionality",
                nameof(componentType));
        }
    }

    private static object CreateEmptyInstance(Type type) =>
        RuntimeHelpers.GetUninitializedObject(type);

    private static string GetPropertyKeyOrThrow(object? key, Type componentType, string memberName) =>
        key as string
        ?? throw new InvalidOperationException(
            $"Component '{componentType.FullName}' received a non-string property key ('{key ?? "<null>"}') while resolving member '{memberName}'. Check whether the template schema is correct.");

    // Fast path first (no runtime generic instantiation, AOT-safe); the general provider only sees exotic conversions.
    private object? ConvertScalar(object srcValue, Type destType) =>
        AotValueConverter.TryConvert(srcValue, destType, StringConverters, out var converted)
            ? converted
            : _typeConversionProvider.Convert(srcValue.GetType(), destType, srcValue);

    private object? ConvertValueFromSrcToDestType(object srcValue, Type destType)
    {
        if (srcValue == null)
        {
            throw new InvalidOperationException(
                $"Component '{destType.FullName}' received a null value. Check whether the template schema is correct.");
        }

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
                convertResult = ConvertScalar(srcValue, destType);
                break;
        }

        return convertResult;
    }
}
