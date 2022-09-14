using System.Diagnostics.CodeAnalysis;

namespace RoguelikeToolkit.Entities.Exceptions
{
	/// <inheritdoc />
	public class TemplateAlreadyExistsException : Exception
	{
		/// <inheritdoc />
		[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1648:InheritDocMustBeUsedWithInheritingClass", Justification = "Reviewed.")]
		public TemplateAlreadyExistsException(string templateName)
			: base($"Template (name = {templateName}) already exists. Note that 'foo.yaml' and 'foo.json' would be considered as the same template")
		{
		}
	}
}
