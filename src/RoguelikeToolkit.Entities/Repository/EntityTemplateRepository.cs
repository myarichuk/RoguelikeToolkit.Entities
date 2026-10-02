using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security;
using RoguelikeToolkit.Entities.Exceptions;
using RoguelikeToolkit.Entities.Extensions;

namespace RoguelikeToolkit.Entities.Repository;

/// <summary>
/// An abstraction of a collection of entity template files. May or may not be composed of multiple folders or single files.
/// Thread-safety: reads (<see cref="TryGetByName"/>, <see cref="GetByTags"/>) are lock-free and may run
/// concurrently with loads. Parsing happens off to the side (in parallel for folders); single-template
/// loads become visible immediately, while a folder load commits its batch one template at a time, so a
/// concurrent reader may observe a prefix of the batch. A failed folder load commits nothing.
/// </summary>
public class EntityTemplateRepository
{
    private static readonly HashSet<string> ValidExtensions = new(StringComparer.OrdinalIgnoreCase) { ".yaml", ".json" };
    private readonly EntityTemplateLoader _loader = new();
    private readonly ConcurrentDictionary<string, EntityTemplate> _entityRepository = new(StringComparer.InvariantCultureIgnoreCase);

    // Inverted tag index: tag -> template names. Built incrementally at load time so that
    // GetByTags avoids a full repository scan. Reflects tags as of load; post-load template
    // mutation (e.g. MergeWith) is not tracked.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _tagIndex = new(StringComparer.InvariantCultureIgnoreCase);

    /// <summary>
    /// Gets the list of all template names present in the repository
    /// </summary>
    public IEnumerable<string> TemplateNames => _entityRepository.Keys;

    /// <summary>
    /// Try to get an in-memory representation of an entity template
    /// </summary>
    /// <param name="templateName">name of the template to fetch</param>
    /// <param name="template">template in-memory representation (that will be fetched)</param>
    /// <returns>true if template with such name was found, false otherwise</returns>
    /// <exception cref="ArgumentNullException"><paramref name="templateName"/> is <see langword="null"/></exception>
    /// <exception cref="FailedToParseException">The template seems to be loaded but it is null, probably due to parsing errors.</exception>
    public bool TryGetByName(string templateName, out EntityTemplate template)
    {
        ArgumentGuard.ThrowIfNull(templateName, nameof(templateName));

        var hasFound = _entityRepository.TryGetValue(templateName, out template!);

        // precaution
        if (hasFound && template == null)
        {
            throw new FailedToParseException(templateName, "The template seems to be loaded but it is null, probably due to parsing errors. This is not supposed to happen and is likely a bug.");
        }

        // Note: template names are backfilled at load time (see LoadTemplate); do not mutate the cached instance here.
        return hasFound;
    }

    /// <summary>
    /// Get one or more template by matching the tags in the template definition to the parameter.
    /// Served by an inverted tag index maintained at load time (cost is proportional to the number of
    /// matching templates, not the repository size). Tag matching is case-insensitive. An empty
    /// <paramref name="tags"/> query returns all templates.
    /// </summary>
    /// <param name="tags">Tags that must be present in the template to fetch it</param>
    /// <returns>A collection of entity templates that contain ALL of the specified tags</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tags"/> or any of it's items is <see langword="null"/></exception>
    public IEnumerable<EntityTemplate> GetByTags(params string[] tags)
    {
        ArgumentGuard.ThrowIfNull(tags, nameof(tags));

        if (tags.Any(t => t == null))
        {
            throw new ArgumentNullException(nameof(tags), "one or more of the tags is null, this is not supported");
        }

        if (tags.Length == 0)
        {
            return _entityRepository.Values.ToList();
        }

        HashSet<string>? matchingNames = null;
        foreach (var tag in tags.OrderBy(tag => _tagIndex.TryGetValue(tag, out var names) ? names.Count : 0))
        {
            if (!_tagIndex.TryGetValue(tag, out var names))
            {
                return Enumerable.Empty<EntityTemplate>();
            }

            if (matchingNames == null)
            {
                matchingNames = new HashSet<string>(names.Keys, StringComparer.InvariantCultureIgnoreCase);
            }
            else
            {
                matchingNames.IntersectWith(names.Keys);
            }

            if (matchingNames.Count == 0)
            {
                return Enumerable.Empty<EntityTemplate>();
            }
        }

        var result = new List<EntityTemplate>(matchingNames!.Count);
        foreach (var name in matchingNames)
        {
            if (_entityRepository.TryGetValue(name, out var template))
            {
                result.Add(template);
            }
        }

        return result;
    }

    /// <summary>
    /// Load template into the repository from a stream
    /// </summary>
    /// <param name="templateName">name of the template to assign when storing it in the repository</param>
    /// <param name="reader">A stream reader to load the template from</param>
    /// <exception cref="TemplateAlreadyExistsException">Template with specified name already exists.</exception>
    /// <exception cref="OverflowException">The repository cache contains too many elements.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="templateName"/> is <see langword="null"/></exception>
    /// <exception cref="FailedToParseException">Failed to parse the template for any reason.</exception>
    public void LoadTemplate(string templateName, StreamReader reader)
    {
        ArgumentGuard.ThrowIfNull(templateName, nameof(templateName));

        var template = _loader.LoadFrom(reader)
            ?? throw new FailedToParseException(templateName, "The template loaded as null, probably due to parsing errors. This is not supposed to happen and is likely a bug.");

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            template.Name = templateName;
        }

        if (!_entityRepository.TryAdd(templateName, template))
        {
            throw new TemplateAlreadyExistsException(templateName);
        }

        template.MarkShared();
        AddToTagIndex(templateName, template);
    }

    /// <summary>
    /// Load template into the repository from an in-memory string (yaml or json content).
    /// Useful for tests, mods received over the network, and generated templates.
    /// </summary>
    /// <param name="templateName">name of the template to assign when storing it in the repository</param>
    /// <param name="templateContent">template content in yaml or json format</param>
    /// <exception cref="TemplateAlreadyExistsException">Template with specified name already exists.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="templateName"/> or <paramref name="templateContent"/> is <see langword="null"/></exception>
    /// <exception cref="FailedToParseException">Failed to parse the template for any reason.</exception>
    public void LoadTemplate(string templateName, string templateContent)
    {
        ArgumentGuard.ThrowIfNull(templateName, nameof(templateName));
        ArgumentGuard.ThrowIfNull(templateContent, nameof(templateContent));

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(templateContent));
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        LoadTemplate(templateName, reader);
    }

    /// <summary>
    /// Add an already built template to the repository under the specified name.
    /// A defensive copy is stored, so later mutations of <paramref name="template"/>
    /// never affect the repository (clone-on-write at the ownership boundary).
    /// Use <see cref="EntityTemplate.Copy"/> to derive variants ("different mobs")
    /// from repository templates and store them back through this method.
    /// </summary>
    /// <param name="templateName">name of the template to assign when storing it in the repository</param>
    /// <param name="template">template to store a copy of</param>
    /// <exception cref="TemplateAlreadyExistsException">Template with specified name already exists.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="templateName"/> or <paramref name="template"/> is <see langword="null"/></exception>
    public void AddTemplate(string templateName, EntityTemplate template)
    {
        ArgumentGuard.ThrowIfNull(templateName, nameof(templateName));
        ArgumentGuard.ThrowIfNull(template, nameof(template));

        var stored = template.Copy();
        if (string.IsNullOrWhiteSpace(stored.Name))
        {
            stored.Name = templateName;
        }

        if (!_entityRepository.TryAdd(templateName, stored))
        {
            throw new TemplateAlreadyExistsException(templateName);
        }

        stored.MarkShared();
        AddToTagIndex(templateName, stored);
    }

    /// <summary>
    /// Remove a template from the repository, together with its tag index entries.
    /// Returns false when no template with such name exists. After removal, call
    /// <c>EntityFactory.InvalidateEffectiveTemplateCache</c> on live factories: cached
    /// effective templates may still reference the removed template.
    /// </summary>
    /// <param name="templateName">name of the template to remove</param>
    /// <returns>true if a template was removed, false otherwise</returns>
    /// <exception cref="ArgumentNullException"><paramref name="templateName"/> is <see langword="null"/></exception>
    public bool RemoveTemplate(string templateName)
    {
        ArgumentGuard.ThrowIfNull(templateName, nameof(templateName));

        if (!_entityRepository.TryRemove(templateName, out var removed))
        {
            return false;
        }

        RemoveFromTagIndex(templateName, removed);
        return true;
    }

    /// <summary>
    /// Load template into the repository from a file.
    /// The template name is derived from the file name with only the last extension stripped
    /// (e.g. <c>template-only-tags.test.foobar.yaml</c> loads as <c>template-only-tags.test.foobar</c>).
    /// Extension matching is case-insensitive (<c>.yaml</c>, <c>.YAML</c>, <c>.json</c>, ...).
    /// </summary>
    /// <param name="templateFile">A file to load the template from</param>
    /// <exception cref="FileNotFoundException">Template file not found</exception>
    /// <exception cref="DirectoryNotFoundException">The specified path of template file is invalid, such as being on an unmapped drive.</exception>
    /// <exception cref="IOException">The template file is already open.</exception>
    /// <exception cref="UnauthorizedAccessException"><see cref="P:System.IO.FileInfo.Name" /> template file is read-only or is a directory.</exception>
    /// <exception cref="InvalidOperationException">Template files must have either 'yaml' or 'json' extensions</exception>
    /// <exception cref="OutOfMemoryException">The length of the one of the strings overflows the maximum allowed length (<see cref="int.MaxValue" />). This is highly unlikely but still can happen :)</exception>
    /// <exception cref="ArgumentNullException"><paramref name="templateFile"/> is <see langword="null"/></exception>
    /// <exception cref="OverflowException">The repository cache contains too many elements.</exception>
    /// <exception cref="TemplateAlreadyExistsException">Template with specified name already exists.</exception>
    /// <exception cref="FailedToParseException">Failed to parse the template for any reason.</exception>
    public void LoadTemplate(FileInfo templateFile)
    {
        ArgumentGuard.ThrowIfNull(templateFile, nameof(templateFile));

        var (templateName, template) = ParseTemplateFile(templateFile, new EntityTemplateLoader());

        if (!_entityRepository.TryAdd(templateName, template))
        {
            throw new TemplateAlreadyExistsException(templateName);
        }

        template.MarkShared();
        AddToTagIndex(templateName, template);
    }

    /// <summary>
    /// Load template into the repository from a file
    /// </summary>
    /// <param name="templateFilename">A file to load the template from</param>
    /// <exception cref="ArgumentNullException"><paramref name="templateFilename"/> is <see langword="null"/></exception>
    /// <exception cref="SecurityException">The caller does not have the required permission to access template filename</exception>
    /// <exception cref="UnauthorizedAccessException">Access to file specified by templateFilename is denied.</exception>
    /// <exception cref="IOException">The template file is already open.</exception>
    /// <exception cref="FileNotFoundException">Template file not found</exception>
    /// <exception cref="DirectoryNotFoundException">The specified path of template file is invalid, such as being on an unmapped drive.</exception>
    /// <exception cref="OutOfMemoryException">The length of the one of the strings overflows the maximum allowed length (<see cref="int.MaxValue" />). This is highly unlikely but still can happen :)</exception>
    /// <exception cref="OverflowException">The repository cache contains too many elements.</exception>
    /// <exception cref="TemplateAlreadyExistsException">Template with specified name already exists.</exception>
    /// <exception cref="InvalidOperationException">Template files must have either 'yaml' or 'json' extensions</exception>
    /// <exception cref="FailedToParseException">Failed to parse the template for any reason.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LoadTemplate(string templateFilename)
    {
        ArgumentGuard.ThrowIfNull(templateFilename, nameof(templateFilename));

        LoadTemplate(new FileInfo(templateFilename));
    }

    /// <summary>
    /// Load all templates from a folder into the repository.
    /// Files are parsed in parallel and the load is atomic: if any file fails to parse, an
    /// <see cref="AggregateException"/> with one inner exception per failed file is thrown and
    /// the repository is left unchanged.
    /// </summary>
    /// <param name="templateFolder">Folder to load templates from</param>
    /// <exception cref="SecurityException">The caller does not have the required permission for the repository folder.</exception>
    /// <exception cref="PathTooLongException">The specified path of the repository folder exceeds the system-defined maximum length.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="templateFolder"/> is <see langword="null"/></exception>
    /// <exception cref="DirectoryNotFoundException">The specified path of template file is invalid, such as being on an unmapped drive.</exception>
    /// <exception cref="FileNotFoundException">Template file not found</exception>
    /// <exception cref="IOException">The template file is already open.</exception>
    /// <exception cref="UnauthorizedAccessException"><see cref="P:System.IO.FileInfo.Name" /> template file is read-only or is a directory.</exception>
    /// <exception cref="OutOfMemoryException">The length of the one of the strings overflows the maximum allowed length (<see cref="int.MaxValue" />). This is highly unlikely but still can happen :)</exception>
    /// <exception cref="OverflowException">The repository cache contains too many elements.</exception>
    /// <exception cref="TemplateAlreadyExistsException">Template with specified name already exists.</exception>
    /// <exception cref="ArgumentException">If .NET Framework and .NET Core versions older than 2.1: <paramref name="templateFolder" /> contains invalid characters such as ", &lt;, &gt;, or |.</exception>
    /// <exception cref="AggregateException">One or more template files failed to load. The repository is left unchanged.</exception>
    /// <exception cref="FailedToParseException">Failed to parse the template for any reason.</exception>
    /// <exception cref="InvalidOperationException">Template files must have either 'yaml' or 'json' extensions</exception>
    public void LoadTemplateFolder(string templateFolder) =>
        LoadTemplateFolder(templateFolder, CancellationToken.None);

    /// <summary>
    /// Load all templates from a folder into the repository.
    /// Files are parsed in parallel and the load is atomic: if any file fails to parse, an
    /// <see cref="AggregateException"/> with one inner exception per failed file is thrown and
    /// the repository is left unchanged.
    /// </summary>
    /// <param name="templateFolder">Folder to load templates from</param>
    /// <param name="cancellationToken">token to observe while loading</param>
    /// <exception cref="SecurityException">The caller does not have the required permission for the repository folder.</exception>
    /// <exception cref="PathTooLongException">The specified path of the repository folder exceeds the system-defined maximum length.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="templateFolder"/> is <see langword="null"/></exception>
    /// <exception cref="DirectoryNotFoundException">The specified path of template file is invalid, such as being on an unmapped drive.</exception>
    /// <exception cref="FileNotFoundException">Template file not found</exception>
    /// <exception cref="IOException">The template file is already open.</exception>
    /// <exception cref="UnauthorizedAccessException"><see cref="P:System.IO.FileInfo.Name" /> template file is read-only or is a directory.</exception>
    /// <exception cref="OutOfMemoryException">The length of the one of the strings overflows the maximum allowed length (<see cref="int.MaxValue" />). This is highly unlikely but still can happen :)</exception>
    /// <exception cref="OverflowException">The repository cache contains too many elements.</exception>
    /// <exception cref="TemplateAlreadyExistsException">Template with specified name already exists.</exception>
    /// <exception cref="ArgumentException">If .NET Framework and .NET Core versions older than 2.1: <paramref name="templateFolder" /> contains invalid characters such as ", &lt;, &gt;, or |.</exception>
    /// <exception cref="AggregateException">One or more template files failed to load. The repository is left unchanged.</exception>
    /// <exception cref="OperationCanceledException">Loading was cancelled via <paramref name="cancellationToken"/>.</exception>
    /// <exception cref="FailedToParseException">Failed to parse the template for any reason.</exception>
    /// <exception cref="InvalidOperationException">Template files must have either 'yaml' or 'json' extensions</exception>
    public void LoadTemplateFolder(string templateFolder, CancellationToken cancellationToken)
    {
        ArgumentGuard.ThrowIfNull(templateFolder, nameof(templateFolder));

        var di = new DirectoryInfo(templateFolder);
        if (!di.Exists)
        {
            throw new DirectoryNotFoundException($"Template directory not found (path = {templateFolder})");
        }

        var files = EnumerateTemplateFiles(di).ToList();
        cancellationToken.ThrowIfCancellationRequested();

        // parse phase (parallel, no repository mutation so failures leave the repository untouched).
        // loaders are pooled per worker thread: building the YamlDotNet deserializer
        // dominates per-file cost, and instances are confined to one thread each, so no
        // thread-safety claim on the deserializer itself is needed.
        using var loaders = new ThreadLocal<EntityTemplateLoader>(() => new EntityTemplateLoader(), trackAllValues: false);
        var parsed = new (string TemplateName, EntityTemplate Template)[files.Count];
        var errors = new ConcurrentQueue<Exception>();
        Parallel.For(
            0,
            files.Count,
            new ParallelOptions { CancellationToken = cancellationToken },
            i =>
            {
                try
                {
                    parsed[i] = ParseTemplateFile(files[i], loaders.Value!);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    errors.Enqueue(
                        new InvalidOperationException($"Failed to load template file '{files[i].FullName}'. Reason: {e.Message}", e));
                }
            });

        if (!errors.IsEmpty)
        {
            throw new AggregateException(
                $"Failed to load {errors.Count} template file(s) from folder '{templateFolder}'. The repository was left unchanged.",
                errors);
        }

        // commit phase (sequential and pre-validated, so either all templates are added or none)
        var batchNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var (templateName, _) in parsed)
        {
            if (!batchNames.Add(templateName) || _entityRepository.ContainsKey(templateName))
            {
                throw new TemplateAlreadyExistsException(templateName);
            }
        }

        var committed = new List<(string TemplateName, EntityTemplate Template)>(parsed.Length);
        foreach (var (templateName, template) in parsed)
        {
            // Re-check under racing loads: another thread may have stored the same name
            // after validation. On conflict roll back this batch (repository and tag index)
            // so the load stays atomic instead of silently dropping a template.
            if (!_entityRepository.TryAdd(templateName, template))
            {
                foreach (var (committedName, committedTemplate) in committed)
                {
                    _entityRepository.TryRemove(committedName, out _);
                    RemoveFromTagIndex(committedName, committedTemplate);
                }

                throw new TemplateAlreadyExistsException(templateName);
            }

            template.MarkShared();
            AddToTagIndex(templateName, template);
            committed.Add((templateName, template));
        }
    }

    /// <summary>
    /// Load all templates from a folder into the repository asynchronously.
    /// The load is offloaded to the thread pool (parsing is CPU/IO bound) and is atomic:
    /// on failure the repository is left unchanged, see <see cref="LoadTemplateFolder(string, CancellationToken)"/>.
    /// </summary>
    /// <param name="templateFolder">Folder to load templates from</param>
    /// <param name="cancellationToken">token to observe while loading</param>
    /// <returns>a task that completes when all templates are loaded</returns>
    public Task LoadTemplateFolderAsync(string templateFolder, CancellationToken cancellationToken = default) =>
        Task.Run(() => LoadTemplateFolder(templateFolder, cancellationToken), cancellationToken);

    private static bool HasValidExtension(string extension) =>
        ValidExtensions.Contains(extension);

    private static IEnumerable<FileInfo> EnumerateTemplateFiles(DirectoryInfo di) =>
        di.EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(file => HasValidExtension(file.Extension));

    private static (string TemplateName, EntityTemplate Template) ParseTemplateFile(FileInfo templateFile, EntityTemplateLoader loader)
    {
        if (!templateFile.Exists)
        {
            throw new FileNotFoundException("Template file not found", templateFile.FullName);
        }

        if (!HasValidExtension(templateFile.Extension))
        {
            throw new InvalidOperationException($"Template files must have either {string.Join("or", ValidExtensions)} extensions");
        }

        // note: the FileInfo overload (not the stream one) is used so that $ref paths resolve against the file's directory.
        // The loader must not be shared across threads (YamlDotNet deserializer instances are not
        // documented as thread-safe); folder loads pass one loader per worker thread, single-file
        // loads pass a fresh instance.
        var template = loader.LoadFrom(templateFile)
            ?? throw new FailedToParseException(templateFile.FullName, "The template loaded as null, probably due to parsing errors. This is not supposed to happen and is likely a bug.");

        var dot = templateFile.Name.LastIndexOf('.');
        var templateName = dot < 0 ? templateFile.Name : templateFile.Name[..dot];

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            template.Name = templateName;
        }

        return (templateName, template);
    }

    private void AddToTagIndex(string templateName, EntityTemplate template)
    {
        foreach (var tag in template.Tags)
        {
            _tagIndex
                .GetOrAdd(tag, _ => new ConcurrentDictionary<string, byte>(StringComparer.InvariantCultureIgnoreCase))
                .TryAdd(templateName, 0);
        }
    }

    private void RemoveFromTagIndex(string templateName, EntityTemplate template)
    {
        foreach (var tag in template.Tags)
        {
            if (_tagIndex.TryGetValue(tag, out var names))
            {
                names.TryRemove(templateName, out _);
            }
        }
    }
}
