using System.Collections.Concurrent;
using System.Reflection;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// Settable instance properties of a component type, resolved once per type and indexed case-insensitively.
    /// Values are written with plain reflection (<see cref="PropertyInfo.SetValue(object, object)"/> /
    /// <see cref="FieldInfo.SetValue(object, object)"/>), which every runtime supports, AOT ones included:
    /// no IL emission, no expression compilation.
    /// </summary>
    /// <remarks>
    /// Value-type components are handled boxed: setting a member on the box mutates the box itself, which is
    /// what the factory returns. Properties without a setter are written through their compiler-generated
    /// backing field when one exists (get-only auto-properties); other get-only properties are not members.
    /// Fields are intentionally not members (template keys naming fields are reported as unmatched).
    /// </remarks>
    internal sealed class ComponentMembers
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly ConcurrentDictionary<Type, ComponentMembers> Cache = new();

        private readonly Dictionary<string, Member> _byName;

        private ComponentMembers(Type type)
        {
            _byName = new Dictionary<string, Member>(StringComparer.InvariantCultureIgnoreCase);

            // Walk the hierarchy explicitly: GetProperties(NonPublic) does not return private members of base types.
            for (var current = type; current != null && current != typeof(object) && current != typeof(ValueType); current = current.BaseType)
            {
                foreach (var property in current.GetProperties(InstanceMembers | BindingFlags.DeclaredOnly))
                {
                    if (property.GetIndexParameters().Length != 0 || _byName.ContainsKey(property.Name))
                    {
                        // most-derived declaration wins (shadowed/overridden properties)
                        continue;
                    }

                    var member = Member.TryCreate(property, current);
                    if (member != null)
                    {
                        _byName.Add(property.Name, member);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the (cached) member map for <paramref name="type"/>.
        /// </summary>
        /// <param name="type">component type</param>
        /// <returns>member map</returns>
        public static ComponentMembers For(Type type) => Cache.GetOrAdd(type, static t => new ComponentMembers(t));

        /// <summary>
        /// Looks a member up by template key (case-insensitive).
        /// </summary>
        /// <param name="name">template key</param>
        /// <param name="member">resolved member</param>
        /// <returns>true if the type has a settable member with that name</returns>
        public bool TryGet(string name, out Member member) => _byName.TryGetValue(name, out member!);

        /// <summary>
        /// A single settable member.
        /// </summary>
        internal sealed class Member
        {
            private readonly PropertyInfo? _property;
            private readonly FieldInfo? _backingField;

            private Member(Type type, PropertyInfo? property, FieldInfo? backingField)
            {
                Type = type;
                _property = property;
                _backingField = backingField;
            }

            /// <summary>
            /// Gets the declared type of the member.
            /// </summary>
            public Type Type { get; }

            /// <summary>
            /// Writes <paramref name="value"/> into <paramref name="target"/> (a boxed instance for value types).
            /// </summary>
            /// <param name="target">instance to mutate</param>
            /// <param name="value">value already converted to <see cref="Type"/></param>
            public void SetValue(object target, object? value)
            {
                if (_property != null)
                {
                    _property.SetValue(target, value);
                }
                else
                {
                    _backingField!.SetValue(target, value);
                }
            }

            /// <summary>
            /// Creates a member for a settable property, or a get-only auto-property via its backing field.
            /// </summary>
            /// <param name="property">property to wrap</param>
            /// <param name="declaringType">type that declares the property (owns the backing field)</param>
            /// <returns>the member, or null if the property cannot be written</returns>
            internal static Member? TryCreate(PropertyInfo property, Type declaringType)
            {
                if (property.SetMethod != null)
                {
                    return new Member(property.PropertyType, property, null);
                }

                var backingField = declaringType.GetField($"<{property.Name}>k__BackingField", InstanceMembers | BindingFlags.DeclaredOnly);
                return backingField != null ? new Member(property.PropertyType, null, backingField) : null;
            }
        }
    }
}
