namespace RoguelikeToolkit.Entities.Benchmarks;

/// <summary>
/// Bench-only components. Registered via <see cref="ComponentAttribute"/> so the shared
/// <c>ComponentTypeRegistry</c> picks them up just like game components.
/// </summary>
[Component(Name = "BenchHealth")]
public sealed class BenchHealth
{
    /// <summary>Gets or sets the maximum value.</summary>
    public int Max { get; set; }

    /// <summary>Gets or sets the current value.</summary>
    public int Current { get; set; }
}

/// <summary>
/// Bench-only component.
/// </summary>
[Component(Name = "BenchSpeed")]
public sealed class BenchSpeed
{
    /// <summary>Gets or sets the speed value.</summary>
    public int Value { get; set; }
}

/// <summary>
/// Bench-only component.
/// </summary>
[Component(Name = "BenchAttack")]
public sealed class BenchAttack
{
    /// <summary>Gets or sets the damage expression.</summary>
    public string? Damage { get; set; }

    /// <summary>Gets or sets the attack range.</summary>
    public int Range { get; set; }
}

/// <summary>
/// Bench-only component.
/// </summary>
[Component(Name = "BenchArmor")]
public sealed class BenchArmor
{
    /// <summary>Gets or sets the armor value.</summary>
    public int Value { get; set; }
}

/// <summary>
/// Bench-only component.
/// </summary>
[Component(Name = "BenchName")]
public sealed class BenchName
{
    /// <summary>Gets or sets the display name.</summary>
    public string? Text { get; set; }
}
