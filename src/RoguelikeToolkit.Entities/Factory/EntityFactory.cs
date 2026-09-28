using System.Reflection;
using DefaultEcs;
using Fasterflect;
using RoguelikeToolkit.Entities.Exceptions;
using RoguelikeToolkit.Entities.Extensions;
using RoguelikeToolkit.Entities.Repository;

namespace RoguelikeToolkit.Entities.Factory;

/// <summary>
/// A factory class that constructs entities according to the defined templates.
/// Spawn cost per template is kept flat: inheritance is resolved once and cached
/// (see <see cref="EntityInheritanceResolver"/>), and child entities are created by recursing
/// into direct children only — no graph traversal or pooled-buffer rental happens per spawn.
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
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(entityRepository);
        ArgumentNullException.ThrowIfNull(world);
#else
        if (entityRepository == null)
        {
            throw new ArgumentNullException(nameof(entityRepository));
        }

        if (world == null)
        {
            throw new ArgumentNullException(nameof(world));
        }
#endif

        _entityRepository = entityRepository;
        _world = world;
        _inheritanceResolver = new EntityInheritanceResolver(_entityRepository.TryGetByName);

        var componentInEntitySetterTypes = Assembly.GetExecutingAssembly().TypesImplementing<BaseComponentInEntitySetter>();
        _componentInEntitySetters =
            componentInEntitySetterTypes
                .Select(type =>
                    (BaseComponentInEntitySetter)Activator.CreateInstance(type, world)!)
                .ToList();
    }

    /// <summary>
    /// Clears the effective-template cache used when spawning. Plain loading of new templates
    /// never requires this (repositories reject duplicate names); call it after mutating a loaded
    /// template or to reclaim memory (e.g. on mod unload / hot-reload).
    /// </summary>
    public void InvalidateEffectiveTemplateCache() =>
        _inheritanceResolver.InvalidateCache();

    /// <summary>
    /// Check whether specific <paramref name="entityName"/> exists in the repository or not
    /// </summary>
    /// <param name="entityName">name of the entity template to check for</param>
    /// <returns>true if exists, false otherwise</returns>
    /// <exception cref="FailedToParseException">The template seems to be loaded but it is null, probably due to parsing errors.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="entityName"/> is <see langword="null"/></exception>
    public bool HasTemplateFor(string entityName)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(entityName);
#else
        if (entityName == null)
        {
            throw new ArgumentNullException(nameof(entityName));
        }
#endif

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
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(entityName);
#else
        if (entityName == null)
        {
            throw new ArgumentNullException(nameof(entityName));
        }
#endif

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
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(rootTemplate);
#else
        if (rootTemplate == null)
        {
            throw new ArgumentNullException(nameof(rootTemplate));
        }
#endif

        return TryCreateCore(
            rootTemplate,
            out entity,
            new HashSet<EntityTemplate>(ReferenceEqualityComparer<EntityTemplate>.Instance));
    }

    /// <summary>
    /// Try create entity from a specified template. Cyclic embedded template references throw <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="rootTemplate">entity template to use</param>
    /// <param name="entity"><see cref="Entity"/> instance - result of the construction</param>
    /// <param name="creationChain">templates on the current creation stack, used for cycle detection</param>
    /// <returns>true if creation succeeded, false otherwise</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rootTemplate"/> is <see langword="null"/></exception>
    /// <exception cref="InvalidOperationException">Cyclic embedded template reference detected.</exception>
    private bool TryCreateCore(EntityTemplate rootTemplate, out Entity entity, HashSet<EntityTemplate> creationChain)
    {
        if (!creationChain.Add(rootTemplate))
        {
            throw new InvalidOperationException(
                $"Cyclic embedded template reference detected (template = '{rootTemplate.Name ?? "<unnamed>"}'). Check the embedded template hierarchy for a loop.");
        }

        try
        {
            // ReSharper disable once ExceptionNotDocumented (we make sure in code that TryGet template doesn't throw)
            var effectiveRootTemplate = _inheritanceResolver.GetEffectiveTemplate(rootTemplate);

            CreateEntity(effectiveRootTemplate, out var rootEntity);
            entity = CreateChildEntities(effectiveRootTemplate, rootEntity, creationChain);

            return true;
        }
        finally
        {
            creationChain.Remove(rootTemplate);
        }
    }

    private Entity CreateChildEntities(EntityTemplate effectiveRootTemplate, Entity rootEntity, HashSet<EntityTemplate> creationChain)
    {
        // Only direct children are created here; recursion via TryCreateCore handles deeper levels.
        // (Traversing the whole subtree here would duplicate grandchildren, once per ancestor level.)
        // Note: recursion uses the original (shared) child references, so the creation chain reliably
        // detects cycles by reference. Name comparisons are deliberately avoided here: template names
        // are case-insensitive identifiers and a child may legitimately share its parent's name.
        foreach (var childTemplate in effectiveRootTemplate.EmbeddedTemplates)
        {
            if (TryCreateCore(childTemplate, out var childEntity, creationChain))
            {
                rootEntity.SetAsParentOf(childEntity);
            }
        }

        return rootEntity;
    }

    // TODO: refactor to reduce cognitive complexity
    private void CreateEntity(EntityTemplate template, out Entity entity)
    {
        entity = _world.CreateEntity();

        foreach (var componentRawData in template.Components)
        {
            var componentType = GetComponentTypeOrThrow(componentRawData);

            if (componentRawData.Value == null)
            {
                throw new InvalidOperationException(
                    $"Component '{componentRawData.Key}' in template '{template.Name ?? "<unnamed>"}' has a null value. Check whether the template schema is correct.");
            }

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
