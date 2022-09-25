namespace RoguelikeToolkit.Entities
{
    /// <summary>
    /// An attribute that marks an entity component
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class ComponentAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets a value of component name, this will serve as an id in the entity template text file
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether component's instance will be shared globally
        /// </summary>
        public bool IsGlobal { get; set; }
    }
}
