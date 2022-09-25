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
    /// A class to resolve inheritance of a template
    /// </summary>
    internal class EntityInheritanceResolver
    {
        private readonly TryGetTemplateByName _tryGetByName;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityInheritanceResolver"/> class.
        /// </summary>
        /// <param name="getByIdFunc">Strategy for fetching the templates</param>
        public EntityInheritanceResolver(TryGetTemplateByName getByIdFunc) =>
            _tryGetByName = getByIdFunc;

        /// <summary>
        /// Traverse the inheritance chain and calculate resulting template
        /// </summary>
        /// <param name="flatTemplate">"top level" template, start of the inheritance chain</param>
        /// <returns>effective template with all of the inheritance applied</returns>
        /// <exception cref="Exception">TryGetByName delegate throws an exception.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="flatTemplate"/> is <see langword="null"/></exception>
        public EntityTemplate GetEffectiveTemplate(EntityTemplate flatTemplate)
        {
            if (flatTemplate == null)
            {
                throw new ArgumentNullException(nameof(flatTemplate));
            }

            // note: this executes copy constructor (feature of C# records!)
            // note 2: if no copy constructor present, this will create shallow clone (inner properties would be the same)
            var templateCopy = flatTemplate with { };

            foreach (var inheritedTemplateName in flatTemplate.Inherits)
            {
                if (_tryGetByName(inheritedTemplateName, out var inheritedTemplate))
                {
                    var embeddedEffectiveTemplate = GetEffectiveTemplate(inheritedTemplate);
                    MergeInheritedTemplates(embeddedEffectiveTemplate, templateCopy);
                }
                else
                {
                    ThrowOnMissingInheritance(flatTemplate, inheritedTemplateName);
                }
            }

            return templateCopy;
        }

        private static void MergeInheritedTemplates(EntityTemplate srcTemplate, EntityTemplate destTemplate)
        {
            (destTemplate.Components as IDictionary<string, object> ?? throw new InvalidOperationException("Template components should implement IDictionary but they didn't. This is not supposed to happen and is likely a bug."))
                .MergeWith(srcTemplate.Components as IDictionary<string, object> ?? throw new InvalidOperationException("Template components should implement IDictionary but they didn't. This is not supposed to happen and is likely a bug."));
            ((ISet<string>)destTemplate.Tags).UnionWith(srcTemplate.Tags);
            ((ISet<string>)destTemplate.Inherits).UnionWith(srcTemplate.Inherits);
        }

        private static void ThrowOnMissingInheritance(EntityTemplate flatTemplate, string inheritedTemplateName)
        {
            throw new InvalidOperationException(
                $"Inherited template name '{inheritedTemplateName}' in  not found. " +
                $"(check template with name = '{flatTemplate.Name}')");
        }
    }
}
