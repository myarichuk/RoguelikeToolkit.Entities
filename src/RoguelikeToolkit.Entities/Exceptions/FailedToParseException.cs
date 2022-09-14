using System.Diagnostics.CodeAnalysis;

namespace RoguelikeToolkit.Entities.Exceptions;

/// <inheritdoc />
internal class FailedToParseException : Exception
{
	/// <inheritdoc />
	[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1648:InheritDocMustBeUsedWithInheritingClass", Justification = "Reviewed.")]
	public FailedToParseException(string failureReason)
		: base($"Failed to parse a template. Reason: {failureReason}")
	{
	}

	/// <inheritdoc />
	[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1648:InheritDocMustBeUsedWithInheritingClass", Justification = "Reviewed.")]
	public FailedToParseException(string templateName, string failureReason)
		: base($"Failed to parse a template (name = {templateName}). Reason: {failureReason}")
	{
	}
}
