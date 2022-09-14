using System.Collections.Concurrent;
using System.Reflection;
using Fasterflect;
using RoguelikeToolkit.Entities.Exceptions;
using RoguelikeToolkit.Entities.Extensions;

// ReSharper disable UncatchableException
namespace RoguelikeToolkit.Entities.Factory
{
	/// <summary>
	/// A class that scans all referenced assemblies for component types translates type name to concrete .Net type
	/// </summary>
	internal class ComponentTypeRegistry
	{
		private readonly ConcurrentDictionary<string, Type> _typeRegistry = new(StringComparer.InvariantCultureIgnoreCase);

		/// <summary>
		/// Initializes a new instance of the <see cref="ComponentTypeRegistry"/> class
		/// </summary>
		/// <exception cref="InvalidOperationException">Failed to load assemblies present in the process.</exception>
		public ComponentTypeRegistry()
		{
			try
			{
				var nonFrameworkAssemblies =
					from assembly in AppDomain.CurrentDomain.GetAssemblies()
					where !IsFrameworkAssembly(assembly)
					select assembly;

				var componentTypes =
					from type in nonFrameworkAssemblies.SelectMany(assembly => assembly.GetTypes())
					where type.HasAttribute<ComponentAttribute>() || type.IsValueComponentType()
					select type;

				foreach (var type in componentTypes)
				{
					var componentTypeName = type.Name;

					if (type.HasAttribute<ComponentAttribute>())
					{
						componentTypeName = type.Attribute<ComponentAttribute>().Name ?? type.Name;
					}

					if (!_typeRegistry.TryAdd(componentTypeName, type))
					{
						ThrowConflictingComponentType(type);
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
			catch (ReflectionTypeLoadException ex)
			{
				throw new InvalidOperationException("Failed to load component types. This error is not supposed to happen and is likely due to some unforeseen issue.", ex);
			}

			static bool IsFrameworkAssembly(Assembly assembly) =>
				(assembly.FullName?.Contains("Microsoft.") ?? false) ||
				(assembly.FullName?.Contains("System.") ?? false);
		}

		/// <summary>
		/// Try to fetch component type from the registry
		/// </summary>
		/// <param name="typeName">Name of the component (either the object name or the Name attribute of <see cref="ComponentAttribute"/> attribute</param>
		/// <param name="type">fetched component type</param>
		/// <returns>true if the type found in the registry, false otherwise</returns>
		public bool TryGetComponentType(string typeName, out Type? type) =>
			_typeRegistry.TryGetValue(typeName, out type);

		private void ThrowConflictingComponentType(Type conflictingType) =>
			throw new ComponentTypeConflictException(conflictingType.Name, _typeRegistry[conflictingType.Name].FullName);
	}
}
