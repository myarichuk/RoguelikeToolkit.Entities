using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Fasterflect;
using RoguelikeToolkit.Entities.Exceptions;
using RoguelikeToolkit.Entities.Extensions;

// ReSharper disable UncatchableException
namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// A class that scans all referenced assemblies for component types translates type name to concrete .Net type.
    /// The scan runs once per process (shared lazy cache); every instance serves the same data.
    /// Assemblies loaded after the first scan are not picked up.
    /// </summary>
    internal class ComponentTypeRegistry
    {
        private static readonly Lazy<IReadOnlyDictionary<string, Type>> SharedTypeRegistry =
            new(BuildTypeRegistry, LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly IReadOnlyDictionary<string, Type> _typeRegistry = SharedTypeRegistry.Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentTypeRegistry"/> class
        /// </summary>
        /// <exception cref="InvalidOperationException">Failed to load assemblies present in the process.</exception>
        public ComponentTypeRegistry()
        {
        }

        /// <summary>
        /// Try to fetch component type from the registry
        /// </summary>
        /// <param name="typeName">Name of the component (either the object name or the Name attribute of <see cref="ComponentAttribute"/> attribute</param>
        /// <param name="type">fetched component type</param>
        /// <returns>true if the type found in the registry, false otherwise</returns>
        public bool TryGetComponentType(string typeName, out Type? type) =>
            _typeRegistry.TryGetValue(typeName, out type);

        private static Dictionary<string, Type> BuildTypeRegistry()
        {
            var typeRegistry = new Dictionary<string, Type>(StringComparer.InvariantCultureIgnoreCase);

            try
            {
                var executingAssembly = Assembly.GetExecutingAssembly();
                var referenced = executingAssembly.GetReferencedAssemblies();

                // make sure to load all referenced assemblies into the domain so we don't miss any assemblies
                foreach (var asm in referenced)
                {
                    Assembly.Load(asm);
                }

                var entitiesAssemblyFullName = executingAssembly.FullName;

                // component types must live in this assembly or reference it (the attribute/interface are defined here),
                // so assemblies that do neither can be skipped without scanning their types
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (IsFrameworkAssembly(assembly) || !IsCandidateAssembly(assembly, executingAssembly, entitiesAssemblyFullName))
                    {
                        continue;
                    }

                    foreach (var type in GetLoadableTypes(assembly))
                    {
                        if (!type.HasAttribute<ComponentAttribute>() && !type.IsValueComponentType())
                        {
                            continue;
                        }

                        var componentTypeName = type.HasAttribute<ComponentAttribute>()
                            ? type.Attribute<ComponentAttribute>().Name ?? type.Name
                            : type.Name;

                        if (typeRegistry.ContainsKey(componentTypeName))
                        {
                            throw new ComponentTypeConflictException(componentTypeName, typeRegistry[componentTypeName].FullName);
                        }

                        typeRegistry.Add(componentTypeName, type);
                    }
                }
            }
            catch (AppDomainUnloadedException ex)
            {
                throw new InvalidOperationException("Failed to load assemblies present in the process. This is not supposed to happen and is likely a bug.", ex);
            }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException(
                    "Failed to load component types. Are there too many types marked with 'Component' attribute?", ex);
            }

            return typeRegistry;
        }

        private static bool IsFrameworkAssembly(Assembly assembly)
        {
            if (assembly.IsDynamic)
            {
                return true;
            }

            var name = assembly.GetName().Name ?? string.Empty;

            return name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("netstandard", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("Mono.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCandidateAssembly(Assembly assembly, Assembly entitiesAssembly, string? entitiesAssemblyFullName)
        {
            if (ReferenceEquals(assembly, entitiesAssembly))
            {
                return true;
            }

            try
            {
                return assembly.GetReferencedAssemblies().Any(reference => reference.FullName == entitiesAssemblyFullName);
            }
            catch (Exception e) when (e is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                return false;
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null)!;
            }
            catch (Exception e) when (e is FileNotFoundException or FileLoadException or BadImageFormatException or TypeLoadException)
            {
                return Enumerable.Empty<Type>();
            }
        }
    }
}
