using System.Reflection;

namespace RoguelikeToolkit.Entities.Extensions
{
	/// <summary>
	/// A helper class that contains <see cref="System.Type"/> related functions.
	/// </summary>
	internal static class TypeExtensions
	{
		/// <summary>
		/// Return true if a type implements <see cref="IValueComponent{TValue}"/>.
		/// </summary>
		/// <param name="type">Type to check if it implements the <see cref="IValueComponent{TValue}"/> interface.</param>
		/// <returns>Returns <see langword="true" /> if <paramref name="type"/> implements the <see cref="IValueComponent{TValue}"/> interface, <see langword="false" /> otherwise.</returns>
		public static bool IsValueComponentType(this Type type)
		{
			try
			{
				return type.GetInterfaces()
					.Any(i => i.FullName?.Contains("IValueComponent`1") ?? false);
			}
			catch (TargetInvocationException)
			{
				return false;
			}
		}
	}
}
