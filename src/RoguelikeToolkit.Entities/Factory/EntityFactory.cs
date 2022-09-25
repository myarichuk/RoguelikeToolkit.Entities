using System.Reflection;
using DefaultEcs;
using Fasterflect;
using RoguelikeToolkit.Entities.Exceptions;
using RoguelikeToolkit.Entities.Extensions;
using RoguelikeToolkit.Entities.Repository;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// A factory class that constructs entities according to the defined templates
    /// </summary>
    public class EntityFactory
    {
        private readonly EntityTemplateRepository _entityRepository;
        private readonly ComponentFactory _componentFactory = new();
        private readonly ComponentTypeRegistry _componentTypeRegistry = new();
        private readonly EntityInheritanceResolver _inheritanceResolver;
        private readonly World _world;
        private readonly IReadOnlyList<BaseComponentInEntitySetter> _componentInEntitySetters;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityFactory"/> class
        /// </summary>
        /// <param name="entityRepository">template repository that is used to fetch entity templates as needed</param>
        /// <param name="world">DefaultEcs <see cref="World"/> object that creates the <see cref="Entity"/> instances themselves</param>
        /// <exception cref="ArgumentNullException">entityRepository or world parameter is null.</exception>
        public EntityFactory(EntityTemplateRepository entityRepository, World world)
        {
            _entityRepository = entityRepository ?? throw new ArgumentNullException(nameof(entityRepository));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _inheritanceResolver = new EntityInheritanceResolver(_entityRepository.TryGetByName);

            var componentInEntitySetterTypes = Assembly.GetExecutingAssembly().TypesImplementing<BaseComponentInEntitySetter>();
            _componentInEntitySetters =
                componentInEntitySetterTypes
                    .Select(type =>
                        (BaseComponentInEntitySetter)Activator.CreateInstance(type, world)!)
                    .ToList();
        }

        /// <summary>
        /// Check whether specific <paramref name="entityName"/> exists in the repository or not
        /// </summary>
        /// <param name="entityName">name of the entity template to check for</param>
        /// <returns>true if exists, false otherwise</returns>
        /// <exception cref="FailedToParseException">The template seems to be loaded but it is null, probably due to parsing errors.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="entityName"/> is <see langword="null"/></exception>
        public bool HasTemplateFor(string entityName)
        {
            if (entityName == null)
            {
                throw new ArgumentNullException(nameof(entityName));
            }

            return _entityRepository.TryGetByName(entityName, out _);
        }

        /// <summary>
        /// Try create entity from a specified template
        /// </summary>
        /// <param name="entityName">name of the entity template to use</param>
        /// <param name="entity"><see cref="Entity"/> instance - result of the construction</param>
        /// <returns>true if creation succeeded, false otherwise</returns>
        /// <exception cref="ArgumentNullException">templateName is <see langword="null"/></exception>
        /// <exception cref="FailedToParseException">The template seems to be loaded but it is null, probably due to parsing errors.</exception>
        public bool TryCreate(string entityName, out Entity entity)
        {
            if (entityName == null)
            {
                throw new ArgumentNullException(nameof(entityName));
            }

            entity = default;
            return _entityRepository.TryGetByName(entityName, out var rootTemplate) &&
                   TryCreate(rootTemplate, out entity);
        }

        /// <summary>
        /// Try create entity from a specified template
        /// </summary>
        /// <param name="rootTemplate">entity template to use</param>
        /// <param name="entity"><see cref="Entity"/> instance - result of the construction</param>
        /// <returns>true if creation succeeded, false otherwise</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rootTemplate"/> is <see langword="null"/></exception>
        internal bool TryCreate(EntityTemplate rootTemplate, out Entity entity)
        {
            if (rootTemplate == null)
            {
                throw new ArgumentNullException(nameof(rootTemplate));
            }

            // ReSharper disable once ExceptionNotDocumented (we make sure in code that TryGet template doesn't throw)
            var effectiveRootTemplate = _inheritanceResolver.GetEffectiveTemplate(rootTemplate);

            var graphIterator = new EmbeddedTemplateGraphIterator(effectiveRootTemplate);

            ConstructEntity(effectiveRootTemplate, out var rootEntity);

            graphIterator.Traverse(template =>
            {
                if (template.Name == effectiveRootTemplate.Name)
                {
                    return;
                }

                template = _inheritanceResolver.GetEffectiveTemplate(template);
                if (TryCreate(template, out var childEntity))
                {
                    rootEntity.SetAsParentOf(childEntity);
                }
            });

            entity = rootEntity;

            return true;
        }

        // TODO: refactor to reduce cognitive complexity
        private void ConstructEntity(EntityTemplate template, out Entity entity)
        {
            entity = _world.CreateEntity();

            foreach (var componentRawData in template.Components)
            {
                var componentType = GetComponentTypeOrThrow(componentRawData);

                var rawComponentType = componentRawData.Value.GetType();
                var componentInstance = CreateComponentInstance(rawComponentType, componentType, componentRawData);

                foreach (var componentSetter in _componentInEntitySetters)
                {
                    if (componentSetter.CanSetComponent(componentType))
                    {
                        componentSetter.SetComponent(entity, componentType, componentInstance);
                        break;
                    }
                }
            }
        }

        private Type GetComponentTypeOrThrow(KeyValuePair<string, object> componentRawData)
        {
            if (!_componentTypeRegistry.TryGetComponentType(componentRawData.Key, out var componentType))
            {
                throw new InvalidOperationException(
                    $"Component type '{componentRawData.Key}' is not registered. Check the spelling of the component name in the template.");
            }

            if (componentType == null)
            {
                throw new InvalidOperationException(
                    "A value in internal cache is null and this is supposed to happen. This is likely a bug.");
            }

            return componentType;
        }

        private object CreateComponentInstance(Type componentRawType, Type componentType, KeyValuePair<string, object> componentRawData)
        {
            object? componentInstance;
            if (componentRawType.IsValueType || componentRawType.Name == nameof(String))
            {
                // TODO: refactor for better error handling
                if (!_componentFactory.TryCreateValueInstance(componentType, componentRawData.Value, out componentInstance))
                {
                    throw new InvalidOperationException(
                        $"Failed to create an instance of a component (type = {componentType.FullName})");
                }
            }
            else
            {
                if (componentRawData.Value is not Dictionary<object, object> componentObjectData)
                {
                    throw new InvalidOperationException(
                        "Invalid data received from the template after deserialization. This is not supposed to happen and is likely a bug.");
                }

                // TODO: refactor for better error handling
                if (!_componentFactory.TryCreateReferenceInstance(componentType, componentObjectData, out componentInstance))
                {
                    throw new InvalidOperationException(
                        $"Failed to create an instance of a component (type = {componentType.FullName})");
                }
            }

            return componentInstance ?? throw new InvalidOperationException(
                "Failed to create component instance. This is not supposed to happen and is likely a bug.");
        }
    }
}
