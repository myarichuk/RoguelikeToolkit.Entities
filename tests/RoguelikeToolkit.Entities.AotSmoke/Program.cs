using DefaultEcs;
using RoguelikeToolkit.DiceExpression;
using RoguelikeToolkit.Entities;
using RoguelikeToolkit.Entities.Components;
using RoguelikeToolkit.Entities.Factory;
using RoguelikeToolkit.Entities.Repository;

// Statically known closed generics: what a stricter AOT runtime needs (optional on IL2CPP 2022.1+).
AotComponentRegistry.Register<Health>();
AotComponentRegistry.Register<Stats>();
AotComponentRegistry.Register<Inventory>();
AotComponentRegistry.Register<Speed>();
AotComponentRegistry.Register<Weapon>();
AotComponentRegistry.Register<Rules>();

var dir = Path.Combine(Path.GetTempPath(), "rltk-aot-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
File.WriteAllText(Path.Combine(dir, "EnemyBase.yaml"), """
Tags: [AI, CanMove]
Components:
  Health: { Max: 10, Current: 10 }
  Speed: 5
  Rules: { Difficulty: hard }
""");
File.WriteAllText(Path.Combine(dir, "Goblin.yaml"), """
Inherits: [EnemyBase]
Tags: [Goblin]
Components:
  Health: { Max: 15 }
  Stats: { Strength: 3, Nested: { Luck: 7, Label: lucky } }
  Inventory: { Gold: 12, Bag: { Slots: 4 } }
  Weapon: { Damage: 1d4+1 }
""");

var warnings = new List<string>();
EntityDiagnostics.WarningHandler = warnings.Add;

var repository = new EntityTemplateRepository();
repository.LoadTemplateFolder(dir);
using var world = new World();
var factory = new EntityFactory(repository, world);

var failures = new List<string>();
void Check(bool ok, string what)
{
    if (!ok)
    {
        failures.Add(what);
    }
}

Check(factory.TryCreate("Goblin", out var goblin), "spawn Goblin");
// inheritance overrides whole components (shallow merge), so Current is not inherited
Check(goblin.Get<Health>().Max == 15 && goblin.Get<Health>().Current == 0, "struct component + inheritance override");
Check(goblin.Get<Speed>().Value == 5, "value component");
Check(goblin.Get<Stats>().Strength == 3 && goblin.Get<Stats>().Nested.Luck == 7 && goblin.Get<Stats>().Nested.Label == "lucky", "nested struct");
Check(goblin.Get<Inventory>().Gold == 12 && goblin.Get<Inventory>().Bag?.Slots == 4, "class component + nested class");
Check(goblin.Get<Weapon>().Damage != null, "dice conversion");
Check(goblin.Get<Rules>().Difficulty == "hard" && world.Get<Rules>().Difficulty == "hard", "global component");

for (var i = 0; i < 1000; i++)
{
    Check(factory.TryCreate("Goblin", out _), "mass spawn");
}

Check(repository.GetByTags("AI", "CanMove").Count() >= 1, "tag query");

Directory.Delete(dir, recursive: true);

// expected: one first-write-wins warning per extra spawn of the global Rules component, nothing else
var unexpected = warnings.Where(w => !w.Contains("first write wins")).ToList();
Check(unexpected.Count == 0, "unexpected warnings: " + string.Join(" | ", unexpected));

if (failures.Count > 0)
{
    Console.WriteLine("AOT SMOKE FAILED: " + string.Join("; ", failures.Distinct()));
    return 1;
}

Console.WriteLine("AOT SMOKE OK");
return 0;

[Component]
public struct Health
{
    public int Max { get; set; }

    public int Current { get; set; }
}

public struct Speed : IValueComponent<int>
{
    public int Value { get; set; }
}

[Component]
public struct Stats
{
    public int Strength { get; set; }

    public Luckiness Nested { get; set; }
}

public struct Luckiness
{
    public int Luck { get; set; }

    public string Label { get; set; }
}

[Component]
public class Inventory
{
    public int Gold { get; set; }

    public Bag? Bag { get; set; }
}

public class Bag
{
    public int Slots { get; set; }
}

[Component]
public class Weapon
{
    public Dice? Damage { get; set; }
}

[Component(IsGlobal = true)]
public class Rules
{
    public string? Difficulty { get; set; }
}
