using DefaultEcs;

namespace RoguelikeToolkit.Entities.Components
{
	/// <summary>
	/// The component holds tags of a certain <see cref="Entity"/>.
	/// </summary>
	internal record struct TagsComponent : IValueComponent<HashSet<string>>
	{
		/// <summary>
		/// gets or sets the value of the component.
		/// </summary>
		public HashSet<string> Value { get; set; }
	}
}
