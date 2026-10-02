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
    private static readonly TypeConversionProvider TypeConversionProvider = Factory.TypeConversionProviderFactory.Create();

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .IgnoreFields()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    /// <summary>
    /// Load template from file.
    /// Relative <c>$ref</c>/<c>$merge-ref</c> paths inside the template are resolved against the directory of <paramref name="file"/>.
    /// </summary>
    /// <param name="file">template file to load</param>
    /// <returns>loaded template</returns>
    /// <exception cref="FileNotFoundException">Failed to find template file at specified path.</exception>
    /// <exception cref="InvalidOperationException">Failed to open template file (environmental reason - path too long, security, etc)</exception>
    /// <exception cref="IOException">Failed to open template file</exception>
    /// <exception cref="FailedToParseException">The template is empty or failed to parse.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityTemplate? LoadFrom(FileInfo file)
    {
        try
        {
            var referenceChain = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.GetFullPath(file.FullName),
            };

            return LoadFrom(file, referenceChain);
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
    /// Load template from a stream. Relative <c>$ref</c>/<c>$merge-ref</c> paths are resolved
    /// against the process working directory since a stream carries no directory context.
    /// Prefer the <see cref="FileInfo"/> overload when loading from files.
    /// </summary>
    /// <param name="sr">template file to load</param>
    /// <returns>loaded template</returns>
    /// <exception cref="FailedToParseException">Failed to parse the template for any reason.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityTemplate? LoadFrom(StreamReader sr) =>
        LoadFrom(sr, baseDirectory: null, referenceChain: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private static bool TryHandlePropertyValue(EntityTemplate template, string propertyName, object? propertyValue, out string? failureReason)
    {
        failureReason = null;
        switch (propertyName)
        {
            case nameof(EntityTemplate.Tags):
                if (propertyValue is not List<object> tagsObjects)
                {
                    failureReason = "Property 'Tags' must be a list of strings.";
                    return false;
                }

                foreach (var tagObject in tagsObjects)
                {
                    if (tagObject is not string tag)
                    {
                        failureReason = $"Property 'Tags' must contain only strings (found '{tagObject ?? "<null>"}').";
                        return false;
                    }

                    template.AddTag(tag);
                }

                break;
            case nameof(EntityTemplate.Inherits):
                if (propertyValue is not List<object> inheritsObjects)
                {
                    failureReason = "Property 'Inherits' must be a list of strings.";
                    return false;
                }

                foreach (var inheritObject in inheritsObjects)
                {
                    if (inheritObject is not string inheritedTemplateName)
                    {
                        failureReason = $"Property 'Inherits' must contain only strings (found '{inheritObject ?? "<null>"}').";
                        return false;
                    }

                    template.AddInherit(inheritedTemplateName);
                }

                break;
            case nameof(EntityTemplate.Components):
                if (propertyValue is not Dictionary<object, object> components)
                {
                    failureReason = "Property 'Components' must be a mapping of component names to component data.";
                    return false;
                }

                foreach (var kvp in components)
                {
                    string componentName;
                    try
                    {
                        componentName = TypeConversionProvider.ConvertToString(kvp.Key);
                    }
                    catch (Exception e)
                    {
                        failureReason = $"Failed to read a component name in 'Components' (key = '{kvp.Key ?? "<null>"}'). Reason: {e.Message}";
                        return false;
                    }

                    if (!template.AddComponent(componentName, kvp.Value))
                    {
                        failureReason = $"Duplicate component name '{componentName}' in 'Components'. Component names must be unique within a template.";
                        return false;
                    }
                }

                break;
            default:
                failureReason = $"Unrecognized property name {propertyName}, this is not supposed to happen and is likely a bug";
                return false;
        }

        return true;
    }

    private static bool IsRefMetaProperty(string key) =>
        key.Equals("$ref", StringComparison.InvariantCultureIgnoreCase);

    private static bool IsMergeRefMetaProperty(string key) =>
        key.Equals("$merge-ref", StringComparison.InvariantCultureIgnoreCase);

    private static string ResolveReferencePath(string referencedPath, string? baseDirectory)
    {
        var combined = Path.IsPathRooted(referencedPath) || string.IsNullOrEmpty(baseDirectory)
            ? referencedPath
            : Path.Combine(baseDirectory, referencedPath);

        return Path.GetFullPath(combined);
    }

    /// <summary>
    /// Load template from file, tracking the <c>$ref</c> resolution chain for cycle detection.
    /// </summary>
    /// <param name="file">template file to load.</param>
    /// <param name="referenceChain">absolute paths of templates currently being resolved above this call.</param>
    /// <returns>loaded template.</returns>
    private EntityTemplate? LoadFrom(FileInfo file, HashSet<string> referenceChain)
    {
        using var fs = file.OpenRead();
        using var sr = new StreamReader(fs);

        return LoadFrom(sr, file.DirectoryName, referenceChain);
    }

    private EntityTemplate? LoadFrom(StreamReader sr, string? baseDirectory, HashSet<string> referenceChain)
    {
        var rawTemplate = _deserializer.Deserialize<Dictionary<string, object>>(sr);

        if (rawTemplate == null || rawTemplate.Count == 0)
        {
            throw new FailedToParseException("The template is empty. Empty templates are not allowed, check whether the template file has valid content.");
        }

        return TryLoadFrom(rawTemplate, baseDirectory, referenceChain, out var template, out var failureReason)
            ? template
            : throw new FailedToParseException(failureReason ?? "unhandled error");
    }

    // ReSharper disable once CognitiveComplexity
    // ReSharper disable once MethodTooLong
    // ReSharper disable once TooManyArguments
    private bool TryLoadFrom(Dictionary<string, object> rawTemplateData, string? baseDirectory, HashSet<string> referenceChain, out EntityTemplate template, out string? failureReason)
    {
        template = new EntityTemplate();
        failureReason = null;

        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        foreach (var kvp in rawTemplateData ?? Enumerable.Empty<KeyValuePair<string, object>>())
        {
            if (EntityTemplate.PropertyNames.TryGetValue(kvp.Key, out var properlyCasedPropertyName))
            {
                if (TryHandlePropertyValue(template, properlyCasedPropertyName, kvp.Value, out var propertyFailureReason))
                {
                    continue;
                }

                failureReason = propertyFailureReason;
                return false;
            }

            if (kvp.Key is { } keyAsString &&
                (IsRefMetaProperty(keyAsString) || IsMergeRefMetaProperty(keyAsString)))
            {
                if (kvp.Value is not string referencedTemplateFilename)
                {
                    failureReason = $"Meta-property '{keyAsString}' must specify a template file path as a string.";
                    return false;
                }

                // throws FileNotFoundException/FailedToParseException/IOException on problems, never silently drops the reference
                TryHandleMetaProperty(template, referencedTemplateFilename, keyAsString, baseDirectory, referenceChain);
                continue;
            }

            // we have a embedded template
            if (kvp.Value is Dictionary<object, object> rawEmbeddedTemplate)
            {
                if (TryHandleEmbeddedTemplate(template, kvp.Key, rawEmbeddedTemplate, baseDirectory, referenceChain, out var templateLoadFailureReason))
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

    // ReSharper disable once TooManyArguments
    private void TryHandleMetaProperty(EntityTemplate template, string referencedTemplateFilename, string keyAsString, string? baseDirectory, HashSet<string> referenceChain)
    {
        var resolvedPath = ResolveReferencePath(referencedTemplateFilename, baseDirectory);

        if (!referenceChain.Add(resolvedPath))
        {
            throw new FailedToParseException($"Cyclic template reference detected ('{referencedTemplateFilename}'). Check the '$ref'/'$merge-ref' chain for a loop.");
        }

        try
        {
            if (IsRefMetaProperty(keyAsString))
            {
                var embeddedTemplate = LoadFrom(new FileInfo(resolvedPath), referenceChain)
                    ?? throw new FailedToParseException($"Referenced template '{referencedTemplateFilename}' loaded as null. This is not supposed to happen and is likely a bug.");

                embeddedTemplate.Name = referencedTemplateFilename;
                if (!template.AddEmbeddedTemplate(embeddedTemplate))
                {
                    throw new FailedToParseException($"Duplicate embedded template name '{embeddedTemplate.Name}'. Embedded template names must be unique within a template.");
                }

                return;
            }

            if (IsMergeRefMetaProperty(keyAsString))
            {
                var embeddedTemplate = LoadFrom(new FileInfo(resolvedPath), referenceChain)
                    ?? throw new FailedToParseException($"Referenced template '{referencedTemplateFilename}' loaded as null. This is not supposed to happen and is likely a bug.");

                embeddedTemplate.Name = referencedTemplateFilename;
                template.MergeWith(embeddedTemplate);

                return;
            }

            throw new FailedToParseException($"Unrecognized meta-property '{keyAsString}' in a template field. Property name must be either '$ref' or '$merge-ref'");
        }
        finally
        {
            referenceChain.Remove(resolvedPath);
        }
    }

    // ReSharper disable once TooManyArguments
    private bool TryHandleEmbeddedTemplate(EntityTemplate template, string embeddedTemplateName, Dictionary<object, object> rawTemplateData, string? baseDirectory, HashSet<string> referenceChain, out string? failureReason)
    {
        failureReason = null;
        Dictionary<string, object> convertedTemplateData;
        try
        {
            convertedTemplateData = rawTemplateData.ToDictionary(
                valuePair => TypeConversionProvider.ConvertToString(valuePair.Key), valuePair => valuePair.Value);
        }
        catch (ArgumentException e)
        {
            failureReason = $"Duplicate embedded property names after key conversion in embedded template '{embeddedTemplateName}'. Reason: {e.Message}";
            return false;
        }

        if (!TryLoadFrom(
                convertedTemplateData, baseDirectory, referenceChain, out var embeddedTemplate, out var loadFailureReason))
        {
            failureReason = loadFailureReason;
            return false;
        }

        embeddedTemplate.Name = embeddedTemplateName;
        if (!template.AddEmbeddedTemplate(embeddedTemplate))
        {
            failureReason = $"Duplicate embedded template name '{embeddedTemplateName}'. Embedded template names must be unique within a template.";
            return false;
        }

        return true;
    }
}
