using System.Collections.Concurrent;
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

    private readonly ObjectMemberIterator _memberIterator = new();

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

        instance = default;
        var instanceAsObject = CreateEmptyInstance(componentType);

        // ReSharper disable once ExceptionNotDocumented
        instanceAsObject.SetPropertyValue(
            nameof(IValueComponent<object>.Value),
            _typeConversionProvider.Convert(
                objectData.GetType(),
                GetValueComponentPayloadType(componentType),
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
    /// <remarks>This overload is intended for object components with properties.
    /// Failures throw (invalid input, conversion errors) rather than returning false.</remarks>
    public bool TryCreateReferenceInstance(Type componentType, IReadOnlyDictionary<object, object> objectData, out object? instance)
    {
        ValidateNonValueComponentInputThrowIfNeeded(componentType, objectData);
        instance = default;
        var instanceAsObject = CreateEmptyInstance(componentType);

        // Index once: every member lookup below is O(1) instead of an O(P) scan.
        var topIndex = BuildKeyIndex(objectData, componentType);
        var consumedTopKeys = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        _memberIterator.Traverse(
            instanceAsObject,
            (in MemberAccessor accessor) =>
            {
                var memberName = accessor.Name;

                // Materialize once: the old code enumerated the path repeatedly (Any/Foreach/Last-in-loop).
                var propertyPath = accessor.PropertyPath.Select(x => x.Name).ToArray();
                var relevantPair = GetRelevantPair(propertyPath, memberName, topIndex, consumedTopKeys);

                if (relevantPair.Value != null)
                {
                    var convertedValue = ConvertValueFromSrcToDestType(
                        relevantPair.Value,
                        accessor.Type);
                    if (convertedValue != null)
                    {
                        accessor.SetValue(convertedValue);
                    }
                    else
                    {
                        EntityDiagnostics.Warn(
                            $"Component '{componentType.FullName}' member '{memberName}' resolved a value that converted to null; the value was skipped. Check whether the template schema is correct.");
                    }
                }
            },
            (in MemberAccessor accessor) => accessor.MemberType == MemberType.Property);

        // Anything left over matches no member (typo, removed property, field instead of property):
        // previously silently ignored, now reported through the diagnostics hook.
        foreach (var key in topIndex.Keys)
        {
            if (!consumedTopKeys.Contains(key))
            {
                EntityDiagnostics.Warn(
                    $"Component '{componentType.FullName}' has no member for template property '{key}'; the value was ignored. Check whether the template schema is correct.");
            }
        }

        instance = instanceAsObject.UnwrapIfWrapped();
        return true;

        KeyValuePair<object, object> GetRelevantPair(
            string[] propertyPath,
            string memberName,
            Dictionary<string, KeyValuePair<object, object>> rootIndex,
            HashSet<string> consumedKeys)
        {
            if (propertyPath.Length == 0)
            {
                if (rootIndex.TryGetValue(memberName, out var pair))
                {
                    consumedKeys.Add(memberName);
                    return pair;
                }

                return default;
            }

            if (!rootIndex.TryGetValue(propertyPath[0], out var topPair))
            {
                return default;
            }

            consumedKeys.Add(propertyPath[0]);

            var currentPair = topPair;
            for (var i = 1; i < propertyPath.Length; i++)
            {
                if (currentPair.Value is not IReadOnlyDictionary<object, object> currentData)
                {
                    return currentPair;
                }

                if (!BuildKeyIndex(currentData, componentType).TryGetValue(propertyPath[i], out currentPair))
                {
                    return default;
                }
            }

            return currentPair;
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

    private static Type GetValueComponentPayloadType(Type componentType) =>
        ValueComponentPayloadCache.GetOrAdd(
            componentType,
            static type => type.GetInterfaces()
                .FirstOrDefault(i => i.FullName?.Contains(nameof(IValueComponent<object>)) ?? false)
                ?.GenericTypeArguments[0]
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
        RuntimeHelpers.GetUninitializedObject(type).WrapIfValueType();

    private static string GetPropertyKeyOrThrow(object? key, Type componentType, string memberName) =>
        key as string
        ?? throw new InvalidOperationException(
            $"Component '{componentType.FullName}' received a non-string property key ('{key ?? "<null>"}') while resolving member '{memberName}'. Check whether the template schema is correct.");

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
                convertResult =
                    _typeConversionProvider.Convert(srcValue.GetType(), destType, srcValue);
                break;
        }

        return convertResult;
    }
}
