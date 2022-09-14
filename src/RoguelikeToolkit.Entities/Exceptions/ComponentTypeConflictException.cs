using System;
using System.Diagnostics.CodeAnalysis;

namespace RoguelikeToolkit.Entities.Exceptions;

/// <inheritdoc />
internal class ComponentTypeConflictException : Exception
{
	/// <inheritdoc />
	[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1648:InheritDocMustBeUsedWithInheritingClass", Justification = "Reviewed.")]
	public ComponentTypeConflictException(string conflictingTypeName, string existingFullTypeName)
		: base($"Failed to add component type, component with name '{conflictingTypeName}' already exist. (fully qualified type of the other, conflicting assembly is {existingFullTypeName})")
	{
	}
}
