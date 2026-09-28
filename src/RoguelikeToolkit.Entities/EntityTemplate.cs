using System.Runtime.CompilerServices;
using DefaultEcs;
using Fasterflect;
using RoguelikeToolkit.Entities.Extensions;
using YamlDotNet.Serialization;

namespace RoguelikeToolkit.Entities
{
    /// <summary>
    /// A class record that holds the structure of the <see cref="Entity"/> to be constructed
    /// </summary>
    public record EntityTemplate
    {
        /// <summary>
        /// Cached property names of the <see cref="EntityTemplate"/> class, used in entity construction logic
        /// </summary>
        internal static readonly HashSet<string> PropertyNames =
            new(
                typeof(EntityTemplate).Properties(Flags.InstancePublic)
                                               .Select(p => p.Name)
                                               .Where(propertyName => propertyName != nameof(EmbeddedTemplates)),
                StringComparer.InvariantCultureIgnoreCase);

        private readonly Dictionary<string, object> _components = new(StringComparer.InvariantCultureIgnoreCase);

        private HashSet<string> _inherits = new(StringComparer.InvariantCultureIgnoreCase);
        private readonly HashSet<string> _tags = new(StringComparer.InvariantCultureIgnoreCase);

        private readonly HashSet<EntityTemplate> _embeddedTemplates = new(EqualityComparer);

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityTemplate"/> class.
        /// </summary>
        public EntityTemplate()
        {
        }

        // note: copy constructor needed for "shallow copy" of records

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityTemplate"/> class (copy constructor)
        /// </summary>
        /// <param name="other">Instance of <see cref="EntityTemplate"/>  to copy values from</param>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/></exception>
        protected EntityTemplate(EntityTemplate other)
        {
            // just in case
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            // note: the copy constructor backs "with" expressions, so identity fields must be copied too,
            // otherwise resolved (effective) templates silently lose their name and description
            Name = other.Name;
            Description = other.Description;
            _components = new Dictionary<string, object>(other.Components, StringComparer.InvariantCultureIgnoreCase);
            _inherits = new HashSet<string>(other.Inherits, StringComparer.InvariantCultureIgnoreCase);
            _tags = new HashSet<string>(other.Tags, StringComparer.InvariantCultureIgnoreCase);
            _embeddedTemplates = new HashSet<EntityTemplate>(other.EmbeddedTemplates);
        }

        /// <summary>
        /// Gets the <see cref="IEqualityComparer{T}"/> implementation for <see cref="EntityTemplate"/>, compares two instances by comparing values of the <see cref="Name"/> properties
        /// </summary>
        public static IEqualityComparer<EntityTemplate> EqualityComparer { get; } = new NameEqualityComparer();

        /// <summary>
        /// Gets or sets the name of the entity template. Effectively, this is an entity Id and it should be unique
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the entity template. This is an "echo" field, it is used for UI and such
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets the collection of entity components.
        /// </summary>
        public IReadOnlyDictionary<string, object> Components => _components;

        /// <summary>
        /// Gets or sets a collection of entity template names, from which entity templates this template inherits from
        /// </summary>
#if NET5_0_OR_GREATER
        public IReadOnlySet<string> Inherits
#else
        public ISet<string> Inherits
#endif
        {
            get => _inherits;
            set => _inherits = new(value);
        }

        /// <summary>
        /// Gets a collection of tags attached to this entity
        /// </summary>
#if NET5_0_OR_GREATER
        public IReadOnlySet<string> Tags => _tags;
#else
        public ISet<string> Tags => _tags;
#endif

        /// <summary>
        /// Gets the collection of embedded templates contained in this one
        /// </summary>
        [YamlIgnore]
#if NET5_0_OR_GREATER
        public IReadOnlySet<EntityTemplate> EmbeddedTemplates => _embeddedTemplates;
#else
        public ISet<EntityTemplate> EmbeddedTemplates => _embeddedTemplates;
#endif

        /// <summary>
        /// Merge this template data with other template. Does not override existing values
        /// </summary>
        /// <param name="other">entity template to copy values from</param>
        public void MergeWith(EntityTemplate other)
        {
            MergeComponents(other.Components);
            MergeInherits(other.Inherits);
            MergeTags(other.Tags);
            MergeEmbeddedTemplates(other.EmbeddedTemplates);
        }

        /// <summary>
        /// Merge this template components data with other template. Does not override existing values
        /// </summary>
        /// <param name="otherComponents">components data to copy values from</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MergeComponents(IReadOnlyDictionary<string, object> otherComponents) =>
            _components.MergeWith(otherComponents);

        /// <summary>
        /// Merge this template inheritance data with other template. Does not override existing values
        /// </summary>
        /// <param name="otherInherits">inheritance data to copy values from</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        internal void MergeInherits(IReadOnlySet<string> otherInherits) =>
#else
        internal void MergeInherits(ISet<string> otherInherits) =>
#endif
            _inherits.UnionWith(otherInherits);

        /// <summary>
        /// Merge this template tags data with other template. Does not override existing values
        /// </summary>
        /// <param name="otherTags">tags data to copy values from</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        internal void MergeTags(IReadOnlySet<string> otherTags) =>
#else
        internal void MergeTags(ISet<string> otherTags) =>
#endif
            _tags.UnionWith(otherTags);

        /// <summary>
        /// Merge this template embedded template data with other template. Does not override existing values
        /// </summary>
        /// <param name="otherEmbeddedTemplates">embedded template data to copy values from</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        internal void MergeEmbeddedTemplates(IReadOnlySet<EntityTemplate> otherEmbeddedTemplates) =>
#else
        internal void MergeEmbeddedTemplates(ISet<EntityTemplate> otherEmbeddedTemplates) =>
#endif
            _embeddedTemplates.UnionWith(otherEmbeddedTemplates);

        /// <summary>
        /// Add a single tag to this template. Used by the loader to populate the template without intermediate copies.
        /// </summary>
        /// <param name="tag">tag to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddTag(string tag) =>
            _tags.Add(tag);

        /// <summary>
        /// Add a single inheritance entry to this template. Used by the loader to populate the template without intermediate copies.
        /// </summary>
        /// <param name="inheritedTemplateName">name of the inherited template to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddInherit(string inheritedTemplateName) =>
            _inherits.Add(inheritedTemplateName);

        /// <summary>
        /// Add a single component to this template. Does not override an existing entry with the same name.
        /// </summary>
        /// <param name="componentName">name of the component to add.</param>
        /// <param name="componentData">raw component data to add.</param>
        /// <returns>true if the component was added, false if a component with the same name already exists.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool AddComponent(string componentName, object componentData)
        {
#if NET5_0_OR_GREATER
            return _components.TryAdd(componentName, componentData);
#else
            if (_components.ContainsKey(componentName))
            {
                return false;
            }

            _components.Add(componentName, componentData);
            return true;
#endif
        }

        /// <summary>
        /// Add a single embedded template to this template. Used by the loader to populate the template without intermediate copies.
        /// </summary>
        /// <param name="embeddedTemplate">embedded template to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddEmbeddedTemplate(EntityTemplate embeddedTemplate) =>
            _embeddedTemplates.Add(embeddedTemplate);

        private sealed class NameEqualityComparer : IEqualityComparer<EntityTemplate>
        {
            public bool Equals(EntityTemplate? x, EntityTemplate? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (ReferenceEquals(x, null))
                {
                    return false;
                }

                if (ReferenceEquals(y, null))
                {
                    return false;
                }

                return x.GetType() == y.GetType() && string.Equals(x.Name, y.Name, StringComparison.InvariantCultureIgnoreCase);
            }

            public int GetHashCode(EntityTemplate obj) =>
                obj.Name != null ? StringComparer.InvariantCultureIgnoreCase.GetHashCode(obj.Name) : 0;
        }
    }
}
