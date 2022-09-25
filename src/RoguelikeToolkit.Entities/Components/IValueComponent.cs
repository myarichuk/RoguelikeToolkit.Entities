using DefaultEcs;

namespace RoguelikeToolkit.Entities.Components
{
    /// <summary>
    /// An interface that marks the component as having a single value. Should be used if there is a need for an <see cref="Entity"/> to have a primitive or a string as a component.
    /// </summary>
    /// <typeparam name="TValue">Component's value type.</typeparam>
    public interface IValueComponent<TValue>
    {
        /// <summary>
        /// gets or sets the value of the component.
        /// </summary>
        TValue Value { get; set; }
    }
}
