# RoguelikeTools.Entities

This is a library to allow resolve entity templates for Entity Component System in a similar way [Ultimate Adom](https://www.ultimate-adom.com/index.php/2018/10/25/making-ultimate-adom-moddable-by-using-entity-component-systems/) is doing.

Supports entity template inheritance and creation of entity hierarchies.  
It is using the awesome [DefaultEcs](https://github.com/Doraku/DefaultEcs) library to construct the entities.

## Features

- **YAML and JSON support:** Entity templates can be written in human-readable `.yaml` or `.json` formats.
- **Inheritance:** Templates can inherit from one or more base templates, mixing components and tags together.
- **Embedded templates:** Hierarchies are fully supported, you can embed child entities inside templates.
- **Scripts:** Components can include scripts, which allow data-driven behavior.
- **Dice parsing:** The system seamlessly creates component variables from standard dice notations (`2d6+3`).

## Quick Start

### 1. Define Templates

Create a file named `templates.yaml` in your project containing your entity definitions:

```yaml
# A base template with generic components
EnemyBase:
  Tags:
    - AI
    - CanMove
  Components:
    Health:
      Max: 10
      Current: 10
    Speed:
      Value: 5

# A concrete template that inherits the EnemyBase
Goblin:
  Inherits:
    - EnemyBase
  Tags:
    - Goblin
  Components:
    Attack:
      Damage: "1d4" # This will automatically be parsed into a Dice object!
    Health:
      Max: 15 # Overrides the EnemyBase's Health Max value
```

### 2. Loading Templates & Creating Entities

Load these templates and generate DefaultEcs entities easily:

```csharp
using DefaultEcs;
using RoguelikeToolkit.Entities.Repository;
using RoguelikeToolkit.Entities.Factory;

var world = new World();
var repository = new EntityTemplateRepository();

// Load the templates
repository.LoadTemplateFolder("path/to/my/templates");

// Initialize the factory
var factory = new EntityFactory(repository, world);

// Create the entity!
if (factory.TryCreate("Goblin", out Entity goblinEntity))
{
    // The goblinEntity now has:
    // - Health component (Max: 15, Current: 10)
    // - Speed component (Value: 5)
    // - Attack component (Damage: 1d4)
    // - Tags: "AI", "CanMove", "Goblin"
}
```

## Advanced Examples

### Embedded Entities

You can embed child entities into another template. When `EntityFactory` creates an `OrcRider`, it also creates the embedded `Wolf` and automatically assigns it as a child using `DefaultEcs.Entity.SetAsParentOf`.

```yaml
Wolf:
  Components:
    Speed:
      Value: 10

OrcRider:
  Components:
    Mount:
      Type: "Wolf"
  # Embeds the 'Wolf' template
  Wolf: {}
```

### Querying Templates

```csharp
repository.TryGetByName("Goblin", out var template); // O(1), case-insensitive

// Tag index: cost follows the matches, not the repository size.
// Requires ALL tags; an empty query returns every template.
var meleeEnemies = repository.GetByTags("AI", "CanMove");
```

### Spawning at Scale

Inheritance is resolved once per template and cached, so spawning thousands of
instances of the same blueprint stays flat:

```csharp
for (var i = 0; i < 2000; i++)
{
    factory.TryCreate("Goblin", out var goblin);
}
```

Global components are first-write-wins: the first spawned entity sets the world's
shared copy, later spawns link to it. Dice strings (`"1d4"`) share one parse per
distinct expression; scripts are constructed per entity and never shared.

### Catching Template Typos

A template property that matches no component member (renamed property, field
instead of property) warns instead of silently dropping. Hard failures (unknown
component type, null value, cycles) throw with template + member context.

```csharp
using RoguelikeToolkit.Entities;

EntityDiagnostics.WarningHandler = message => Console.WriteLine($"[template] {message}");
```

### Save / Load

Persist the template name plus dynamic state, then re-spawn and re-apply:

```csharp
// save
Save("Goblin", entity.Get<Health>().Current, entity.Get<Position>());

// load
factory.TryCreate(savedTemplateName, out var entity);
entity.Set(new Health { Current = savedHp, Max = savedMax });
```

### Modding / Hot-Reload

Loading is additive and folder loads are atomic — on any failure an
`AggregateException` (one inner exception per file) is thrown and the repository
is left unchanged:

```csharp
await repository.LoadTemplateFolderAsync("Mods/Extra", cancellationToken);
```

There is no unload: hot-reload a mod by loading it into a fresh
`EntityTemplateRepository` + `EntityFactory` pair and dropping the old one.

### Template Format

Author in YAML (comments, readability), ship JSON or a packed single file for
spawn-time loading — the same loader reads both (`.yaml` / `.json`,
case-insensitive). Relative `$ref` / `$merge-ref` paths resolve against the
referencing file's directory.

## Docs

- [Usage guide](docs/usage.md) — querying, spawning, pooling, save/load, modding, examples.
- [Architecture](docs/architecture.md) — internal creation and inheritance flow.
- [Integration decisions](docs/integrations.md) — why each dependency / perf choice stands.

## Benchmarks & Stress Tests

```bash
dotnet run --project benchmarks/RoguelikeToolkit.Entities.Benchmarks -c Release
dotnet test Library.sln
```

Benchmarks (BenchmarkDotNet, allocation + time) catch perf/allocation regressions;
the test suite includes stress tests (mass spawn, mass load, concurrent reads).
See [benchmarks/README.md](benchmarks/README.md).