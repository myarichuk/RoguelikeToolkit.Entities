using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security;
using RoguelikeToolkit.Entities.Exceptions;

namespace RoguelikeToolkit.Entities.Repository
{
    /// <summary>
    /// An abstraction of a collection of entity template files. May or may not be composed of multiple folders or single files.
    /// </summary>
    public class EntityTemplateRepository
    {
        private static readonly HashSet<string> ValidExtensions = new() { ".yaml", ".json" };
        private readonly EntityTemplateLoader _loader = new();
        private readonly ConcurrentDictionary<string, EntityTemplate> _entityRepository = new(StringComparer.InvariantCultureIgnoreCase);

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
            if (templateName == null)
            {
                throw new ArgumentNullException(nameof(templateName));
            }

            var hasFound = _entityRepository.TryGetValue(templateName, out template!);

            // precaution
            if (hasFound && template == null)
            {
                throw new FailedToParseException(templateName, "The template seems to be loaded but it is null, probably due to parsing errors. This is not supposed to happen and is likely a bug.");
            }

            if (hasFound && string.IsNullOrWhiteSpace(template!.Name))
            {
                template.Name = templateName;
            }

            return hasFound;
        }

        /// <summary>
        /// Get one or more template by matching the tags in the template definition to the parameter
        /// </summary>
        /// <param name="tags">Tags that must be present in the template to fetch it</param>
        /// <returns>A collection of entity templates that contain ALL of the specified tags</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tags"/> or any of it's items is <see langword="null"/></exception>
        public IEnumerable<EntityTemplate> GetByTags(params string[] tags)
        {
            if (tags == null)
            {
                throw new ArgumentNullException(nameof(tags));
            }

            if (tags.Any(t => t == null))
            {
                throw new ArgumentNullException(nameof(tags), "one or more of the tags is null, this is not supported");
            }

            return _entityRepository.Values.Where(t => t.Tags.IsSupersetOf(tags));
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
            if (templateName == null)
            {
                throw new ArgumentNullException(nameof(templateName));
            }

            var template = _loader.LoadFrom(reader);

            if (!_entityRepository.TryAdd(templateName, template))
            {
                throw new TemplateAlreadyExistsException(templateName);
            }
        }

        /// <summary>
        /// Load template into the repository from a file
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
            if (templateFile == null)
            {
                throw new ArgumentNullException(nameof(templateFile));
            }

            if (!templateFile.Exists)
            {
                throw new FileNotFoundException("Template file not found", templateFile.FullName);
            }

            if (!ValidExtensions.Contains(templateFile.Extension))
            {
                throw new InvalidOperationException($"Template files must have either {string.Join("or", ValidExtensions)} extensions");
            }

            using var fs = templateFile.OpenRead();
            using var reader = new StreamReader(fs);

            var dot = templateFile.Name.LastIndexOf('.');
            LoadTemplate(templateFile.Name[..dot], reader);
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
            if (templateFilename == null)
            {
                throw new ArgumentNullException(nameof(templateFilename));
            }

            LoadTemplate(new FileInfo(templateFilename));
        }

        /// <summary>
        /// Load all templates from a folder into the repository
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
        /// <exception cref="ArgumentException">If .NET Framework and .NET Core versions older than 2.1: <paramref name="path" /> contains invalid characters such as ", &lt;, &gt;, or |.</exception>
        /// <exception cref="FailedToParseException">Failed to parse the template for any reason.</exception>
        /// <exception cref="InvalidOperationException">Template files must have either 'yaml' or 'json' extensions</exception>
        public void LoadTemplateFolder(string templateFolder)
        {
            if (templateFolder == null)
            {
                throw new ArgumentNullException(nameof(templateFolder));
            }

            var di = new DirectoryInfo(templateFolder);
            if (!di.Exists)
            {
                throw new DirectoryNotFoundException($"Template directory not found (path = {templateFolder})");
            }

            foreach (var fi in EnumerateTemplateFiles(di))
            {
                LoadTemplate(fi);
            }

            static IEnumerable<FileInfo> EnumerateTemplateFiles(DirectoryInfo di) =>
                di.EnumerateFiles("*.yaml", SearchOption.AllDirectories)
                    .Concat(di.EnumerateFiles("*.json", SearchOption.AllDirectories));
        }
    }
}
