using System.Collections.Concurrent;
using System.Diagnostics;
using RoguelikeToolkit.Entities.Extensions;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// Delegate signature for fetching template by name (strategy pattern)
    /// </summary>
    /// <param name="templateName">name of the template (always used as primary key of the entity templates)</param>
    /// <param name="template">template instance that is fetched</param>
    /// <returns>true if the template is found, false otherwise</returns>
    internal delegate bool TryGetTemplateByName(string templateName, out EntityTemplate template);

    /// <summary>
    /// A class to resolve inheritance of a template.
    /// Resolved (effective) templates are cached: repository-canonical templates by name
    /// (case-insensitive), everything else (unnamed or embedded templates with local names) by
    /// reference. The cache is safe because template repositories are append-only
    /// (a duplicate name fails to load), so a successfully resolved template can never change
    /// through loading. Only failures (missing base, cycles) are never cached. If a template is
    /// mutated after load (e.g. via <see cref="EntityTemplate.MergeWith"/>), call
    /// <see cref="InvalidateCache"/> explicitly. Returned effective templates are shared and
    /// must be treated as read-only.
    /// </summary>
    internal class EntityInheritanceResolver
    {
        private readonly TryGetTemplateByName _tryGetByName;
        private readonly ConcurrentDictionary<string, EntityTemplate> _namedEffectiveCache =
            new(StringComparer.InvariantCultureIgnoreCase);

        private readonly ConcurrentDictionary<EntityTemplate, EntityTemplate> _unnamedEffectiveCache =
            new(ReferenceEqualityComparer<EntityTemplate>.Instance);

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityInheritanceResolver"/> class.
        /// </summary>
        /// <param name="getByIdFunc">Strategy for fetching the templates</param>
        public EntityInheritanceResolver(TryGetTemplateByName getByIdFunc) =>
            _tryGetByName = getByIdFunc;

        /// <summary>
        /// Traverse the inheritance chain and calculate resulting template.
        /// Cyclic inheritance (a template inheriting itself, directly or transitively) throws <see cref="InvalidOperationException"/>.
        /// Successful resolutions are cached (see class remarks); treat the result as read-only.
        /// </summary>
        /// <param name="flatTemplate">"top level" template, start of the inheritance chain</param>
        /// <returns>effective template with all of the inheritance applied</returns>
        /// <exception cref="Exception">TryGetByName delegate throws an exception.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="flatTemplate"/> is <see langword="null"/></exception>
        /// <exception cref="InvalidOperationException">Cyclic template inheritance detected.</exception>
        public EntityTemplate GetEffectiveTemplate(EntityTemplate flatTemplate)
        {
            if (flatTemplate == null)
            {
                throw new ArgumentNullException(nameof(flatTemplate));
            }

            // Only repository-canonical templates (the exact reference the repository holds under
            // this name) use the name tier: embedded templates carry local names that may collide
            // with each other or with repository names while having different content.
            var isCanonical = flatTemplate.Name != null &&
                _tryGetByName(flatTemplate.Name, out var canonical) &&
                ReferenceEquals(canonical, flatTemplate);

            if (isCanonical)
            {
                if (_namedEffectiveCache.TryGetValue(flatTemplate.Name!, out var cached))
                {
                    return cached;
                }
            }
            else if (_unnamedEffectiveCache.TryGetValue(flatTemplate, out var cachedUnnamed))
            {
                return cachedUnnamed;
            }

            var resolved = GetEffectiveTemplate(
                flatTemplate,
                new HashSet<string>(StringComparer.InvariantCultureIgnoreCase),
                new HashSet<EntityTemplate>(ReferenceEqualityComparer<EntityTemplate>.Instance));

            if (isCanonical)
            {
                _namedEffectiveCache.TryAdd(flatTemplate.Name!, resolved);
            }
            else
            {
                _unnamedEffectiveCache.TryAdd(flatTemplate, resolved);
            }

            return resolved;
        }

        /// <summary>
        /// Clears the effective-template cache. Call this after mutating a loaded template;
        /// plain loading of new templates never requires it (repositories reject duplicate names).
        /// </summary>
        public void InvalidateCache()
        {
            _namedEffectiveCache.Clear();
            _unnamedEffectiveCache.Clear();
        }

        private static void MergeInheritedTemplates(EntityTemplate srcTemplate, EntityTemplate destTemplate)
        {
            (destTemplate.Components as IDictionary<string, object> ?? throw new InvalidOperationException("Template components should implement IDictionary but they didn't. This is not supposed to happen and is likely a bug."))
                .MergeWith(srcTemplate.Components as IDictionary<string, object> ?? throw new InvalidOperationException("Template components should implement IDictionary but they didn't. This is not supposed to happen and is likely a bug."));
            ((ISet<string>)destTemplate.Tags).UnionWith(srcTemplate.Tags);
            ((ISet<string>)destTemplate.Inherits).UnionWith(srcTemplate.Inherits);
            destTemplate.MergeEmbeddedTemplates(srcTemplate.EmbeddedTemplates);
        }

        private static void ThrowOnMissingInheritance(EntityTemplate flatTemplate, string inheritedTemplateName)
        {
            throw new InvalidOperationException(
                $"Inherited template name '{inheritedTemplateName}' is not found. " +
                $"(check template with name = '{flatTemplate.Name}')");
        }

        private EntityTemplate GetEffectiveTemplate(EntityTemplate flatTemplate, HashSet<string> nameChain, HashSet<EntityTemplate> referenceChain)
        {
            var tracksByName = flatTemplate.Name != null;
            if (tracksByName)
            {
                if (!nameChain.Add(flatTemplate.Name!))
                {
                    throw new InvalidOperationException(
                        $"Cyclic template inheritance detected (template = '{flatTemplate.Name}'). Check the 'Inherits' chain for a loop.");
                }
            }
            else if (!referenceChain.Add(flatTemplate))
            {
                throw new InvalidOperationException(
                    "Cyclic template inheritance detected (unnamed template). Check the 'Inherits' chain for a loop.");
            }

            try
            {
                // note: this executes copy constructor (feature of C# records!)
                // note 2: if no copy constructor present, this will create shallow clone (inner properties would be the same)
                var templateCopy = flatTemplate with { };

                foreach (var inheritedTemplateName in flatTemplate.Inherits)
                {
                    if (_tryGetByName(inheritedTemplateName, out var inheritedTemplate))
                    {
                        var embeddedEffectiveTemplate = GetEffectiveTemplate(inheritedTemplate, nameChain, referenceChain);
                        MergeInheritedTemplates(embeddedEffectiveTemplate, templateCopy);
                    }
                    else
                    {
                        ThrowOnMissingInheritance(flatTemplate, inheritedTemplateName);
                    }
                }

                return templateCopy;
            }
            finally
            {
                if (tracksByName)
                {
                    nameChain.Remove(flatTemplate.Name!);
                }
                else
                {
                    referenceChain.Remove(flatTemplate);
                }
            }
        }
    }
}
