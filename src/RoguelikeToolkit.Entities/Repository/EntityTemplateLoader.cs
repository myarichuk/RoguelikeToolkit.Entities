using System.Runtime.CompilerServices;
using System.Security;
using deniszykov.TypeConversion;
using Microsoft.Extensions.Options;
using RoguelikeToolkit.Entities.Exceptions;
using YamlDotNet.Serialization;

namespace RoguelikeToolkit.Entities.Repository;

/// <summary>
/// Loader class for <see cref="EntityTemplate"/>, supports both yaml and json file formats
/// </summary>
internal class EntityTemplateLoader
{
    private static readonly TypeConversionProvider TypeConversionProvider = new(Options.Create(new TypeConversionProviderOptions
    {
        Options = ConversionOptions.UseDefaultFormatIfNotSpecified,
    }));

    private static readonly HashSet<string> EmptyHashSet = new();

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .IgnoreFields()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    /// <summary>
    /// Load template from file
    /// </summary>
    /// <param name="file">template file to load</param>
    /// <returns>loaded template</returns>
    /// <exception cref="FileNotFoundException">Failed to find template file at specified path.</exception>
    /// <exception cref="InvalidOperationException">Failed to open template file (environmental reason - path too long, security, etc)</exception>
    /// <exception cref="IOException">Failed to open template file</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityTemplate? LoadFrom(FileInfo file)
    {
        try
        {
            using var fs = file.OpenRead();
            using var sr = new StreamReader(fs);

            return LoadFrom(sr);
        }
        catch (DirectoryNotFoundException e)
        {
            throw new FileNotFoundException($"Failed to find template file at specified path ({file.FullName})", e);
        }
        catch (FileNotFoundException e)
        {
            throw new FileNotFoundException($"Failed to find template file at specified path ({file.FullName})", e);
        }
        catch (IOException e)
        {
            throw new IOException($"Failed to open template file. Under normal conditions this is not supposed to happen and should be reported. Reason: {e.Message}", e);
        }
        catch (Exception e) when (e is PathTooLongException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        {
            throw new InvalidOperationException($"Failed to open template file. Under normal conditions this is not supposed to happen and should be reported. Reason: {e.Message}", e);
        }
    }

    /// <summary>
    /// Load template from file
    /// </summary>
    /// <param name="filePath">template file to load</param>
    /// <returns>loaded template</returns>
    /// <exception cref="InvalidOperationException">Failed to open template file (environmental reason - path too long, security, etc)</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/></exception>
    public EntityTemplate? LoadFrom(string filePath)
    {
        if (filePath == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        try
        {
            return LoadFrom(new FileInfo(filePath));
        }
        catch (SecurityException e)
        {
            throw new InvalidOperationException($"Failed to open template file. Under normal conditions this is not supposed to happen and should be reported. Reason: {e.Message}", e);
        }
        catch (UnauthorizedAccessException e)
        {
            throw new InvalidOperationException($"Failed to open template file. Under normal conditions this is not supposed to happen and should be reported. Reason: {e.Message}", e);
        }
    }

    /// <summary>
    /// Load template from file
    /// </summary>
    /// <param name="sr">template file to load</param>
    /// <returns>loaded template</returns>
    /// <exception cref="FailedToParseException">Failed to parse the template for any reason.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityTemplate? LoadFrom(StreamReader sr)
    {
        var rawTemplate = _deserializer.Deserialize<Dictionary<string, object>>(sr);

        return TryLoadFrom(rawTemplate, out var template, out var failureReason)
            ? template
            : throw new FailedToParseException(failureReason ?? "unhandled error");
    }

    private static bool TryHandlePropertyValue(EntityTemplate template, string propertyName, object propertyValue)
    {
        switch (propertyName)
        {
            case nameof(EntityTemplate.Tags):
                var tagsCollectionValue = propertyValue is List<object> tagsObjects
                    ? new HashSet<string>(tagsObjects.Cast<string>())
                    : EmptyHashSet;
                template.MergeTags(tagsCollectionValue);
                break;
            case nameof(EntityTemplate.Inherits):
                var inheritsCollectionValue = propertyValue is List<object> inheritsObjects
                    ? new HashSet<string>(inheritsObjects.Cast<string>())
                    : EmptyHashSet;
                template.MergeInherits(inheritsCollectionValue);
                break;
            case nameof(EntityTemplate.Components):
                var componentsValue = propertyValue is not Dictionary<object, object> components
                    ? null
                    : components.ToDictionary(
                        kvp => TypeConversionProvider.ConvertToString(kvp.Key),
                        kvp => kvp.Value);
                if (componentsValue == null)
                {
                    return false;
                }

                template.MergeComponents(componentsValue);
                break;
            default:
                return false;
        }

        return true;
    }

    // ReSharper disable once CognitiveComplexity
    // ReSharper disable once MethodTooLong
    private bool TryLoadFrom(Dictionary<string, object> rawTemplateData, out EntityTemplate template, out string? failureReason)
    {
        template = new EntityTemplate();
        failureReason = null;

        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        foreach (var kvp in rawTemplateData ?? Enumerable.Empty<KeyValuePair<string, object>>())
        {
            if (EntityTemplate.PropertyNames.TryGetValue(kvp.Key, out var properlyCasedPropertyName))
            {
                if (TryHandlePropertyValue(template, properlyCasedPropertyName, kvp.Value))
                {
                    continue;
                }

                failureReason = $"Unrecognized property name {kvp.Key}, this is not supposed to happen and is likely a bug";
                return false;
            }

            if (kvp.Key is { } keyAsString &&
                kvp.Value is string referencedTemplateFilename) // just in case
            {
                if (ТryHandleMetaProperty(template, referencedTemplateFilename, keyAsString))
                {
                    continue;
                }

                failureReason = $"Unrecognized meta-property '{keyAsString}' in a template field. Property name must be either '$ref' or '$merge-ref'";
                return false;
            }

            // we have a embedded template
            if (kvp.Value is Dictionary<object, object> rawEmbeddedTemplate)
            {
                if (TryHandleEmbeddedTemplate(template, kvp.Key, rawEmbeddedTemplate, out var templateLoadFailureReason))
                {
                    continue;
                }

                failureReason = templateLoadFailureReason;
                return false;
            }

            failureReason = $"Unexpected property name '{kvp.Key}'. Check whether the template schema is correct";
            return false;
        }

        // make sure ALL required properties were set
        return true;
    }

    private bool ТryHandleMetaProperty(EntityTemplate template, string referencedTemplateFilename, string keyAsString)
    {
        if (IsRefMetaProperty(keyAsString))
        {
            var embeddedTemplate = LoadFrom(referencedTemplateFilename);
            if (embeddedTemplate == null)
            {
                return true;
            }

            embeddedTemplate.Name = referencedTemplateFilename;
            template.EmbeddedTemplates.Add(embeddedTemplate);

            return true;
        }

        if (IsMergeRefMetaProperty(keyAsString))
        {
            var embeddedTemplate = LoadFrom(referencedTemplateFilename);
            if (embeddedTemplate == null)
            {
                return true;
            }

            embeddedTemplate.Name = referencedTemplateFilename;
            template.MergeWith(embeddedTemplate);

            return true;
        }

        return false;

        bool IsRefMetaProperty(string key) =>
            key.Equals("$ref", StringComparison.InvariantCultureIgnoreCase);

        bool IsMergeRefMetaProperty(string key) =>
            key.Equals("$merge-ref", StringComparison.InvariantCultureIgnoreCase);
    }

    // ReSharper disable once TooManyArguments
    private bool TryHandleEmbeddedTemplate(EntityTemplate template, string embeddedTemplateName, Dictionary<object, object> rawTemplateData, out string? failureReason)
    {
        failureReason = null;
        if (!TryLoadFrom(
                rawTemplateData.ToDictionary(valuePair => TypeConversionProvider.ConvertToString(valuePair.Key), valuePair => valuePair.Value), out var embeddedTemplate, out var loadFailureReason))
        {
            failureReason = loadFailureReason;
            return false;
        }

        embeddedTemplate.Name = embeddedTemplateName;
        template.EmbeddedTemplates.Add(embeddedTemplate);

        return true;
    }
}
