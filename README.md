# RoguelikeToolkit.Entities

[![NuGet](https://img.shields.io/nuget/v/RoguelikeToolkit.Entities.svg)](https://www.nuget.org/packages/RoguelikeToolkit.Entities)
[![openupm](https://img.shields.io/npm/v/com.rogueliketoolkit.entities?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.rogueliketoolkit.entities/)

Data-driven entity templates for [DefaultEcs](https://github.com/Doraku/DefaultEcs), in the spirit of
[Ultimate ADOM's moddable ECS](https://www.ultimate-adom.com/index.php/2018/10/25/making-ultimate-adom-moddable-by-using-entity-component-systems/):
describe entities in YAML/JSON, inherit and compose them, spawn them into a `DefaultEcs.World`.

Targets `net10.0` and `netstandard2.1` (Unity 2021.3+, IL2CPP-safe). MIT.

## Install

- **.NET:** `dotnet add package RoguelikeToolkit.Entities`
- **Unity:** see [Unity / IL2CPP](#unity--il2cpp).

## Features

- **YAML and JSON support:** Entity templates can be written in human-readable `.yaml` or `.json` formats.
- **Inheritance:** Templates can inherit from one or more base templates, mixing components and tags together.
- **Embedded templates:** Hierarchies are fully supported, you can embed child entities inside templates.
- **Scripts:** Components can carry JavaScript (Jint) scripts for data-driven behavior; parsed once, shared, AOT-safe.
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
    // - Health component (Max: 15, Current: 0) -- inheritance overrides whole components, not single fields
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
distinct expression; scripts likewise share one parsed, immutable instance per distinct source.

### Scripts

Script-typed component members (`EntityScript`, `EntityComponentScript`, `EntityInteractionScript`, `Script`, all in
`RoguelikeToolkit.Entities.Scripting`) are populated from template strings:

```yaml
Components:
  Behavior:
    OnHit: |
      var h = entity.GetComponent(Health);
      h.Current -= damage;
      entity.SetComponent(Health, h);
    Regen: component.Current += amount;
```

```csharp
[Component]
public class Behavior
{
    public EntityScript? OnHit { get; set; }
    public EntityComponentScript? Regen { get; set; }
}

// one host per thread / simulation loop; it owns the (single) Jint engine
using var host = new ScriptHost(new ScriptHostOptions { ReferenceTypes = new[] { typeof(Health) } });

var behavior = goblin.Get<Behavior>();
behavior.OnHit!.ExecuteOn(host, ref goblin, ("damage", 4));
behavior.Regen!.TryExecuteOn<Health>(host, goblin, ("amount", 2)); // writes the component back via Entity.Set
```

- Scripts are immutable and hold no state: each distinct source is parsed once per process and the instance is shared by
  every entity spawned from the template. Keep state in components.
- Each execution runs in its own function scope (`var`/`function` do not leak between scripts, `return` exits early);
  bindings (`entity`, `component`, `source`/`target`, arguments) are cleared afterwards.
- `entity.HasComponent/GetComponent/SetComponent/RemoveComponent(Type)` take any type exposed via
  `ScriptHostOptions.ReferenceTypes`. Value-type components come back as copies: write them back with `SetComponent`.
- Limits: 500 statements and recursion depth 1024 per execution by default; `Timeout` and `MemoryLimitBytes` are opt-in.
- `ScriptHost` is not thread-safe (neither is a Jint engine); scripts are, and can run on any number of hosts.

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

## Unity / IL2CPP

The `netstandard2.1` build has no Reflection.Emit on the load/spawn path and no `dynamic`, so it runs under IL2CPP.
CI runs a NativeAOT smoke test (stricter than IL2CPP: no JIT, no runtime generic instantiation, full trimming) on
every PR to keep it that way.

### Install

The UPM package `com.rogueliketoolkit.entities` ships the prebuilt `netstandard2.1` DLL. Its third-party
dependencies (DefaultEcs, YamlDotNet, Jint, ...) come from [UnityNuGet](https://github.com/bdovaz/UnityNuGet)
as `org.nuget.*` packages instead of being bundled: Unity refuses two copies of the same DLL, and Jint/YamlDotNet
are shared with `RoguelikeToolkit.World`. Add both scoped registries to `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["com.rogueliketoolkit"]
    },
    {
      "name": "UnityNuGet",
      "url": "https://unitynuget-registry.openupm.com",
      "scopes": ["org.nuget"]
    }
  ],
  "dependencies": {
    "com.rogueliketoolkit.entities": "5.1.0"
  }
}
```

(`openupm add com.rogueliketoolkit.entities` does the OpenUPM half.)

**Without registries:** every GitHub Release also carries `RoguelikeToolkit.Entities-<version>-unity-plugins.zip`
with the whole DLL closure plus a `link.xml`. Unzip into `Assets/Plugins/RoguelikeToolkit.Entities/`, skipping any DLL
your project already has (`System.Runtime.CompilerServices.Unsafe` often is).

### Runtime notes

- **Unity 2022.1+:** works as is.
- **Older IL2CPP / NativeAOT:** register each component type once at startup so its closed generic code exists:
  `AotComponentRegistry.Register<Health>();`
- **Managed code stripping:** the package hands UnityLinker its own `link.xml` (an editor hook; Unity ignores
  `link.xml` files inside packages), preserving this library, DefaultEcs, YamlDotNet, TypeConversion and Jint. Your
  component types (and the types of their nested struct/class members) are populated by reflection only, so they
  need `[Preserve]` or a `link.xml` of your own under `Assets/`:

  ```xml
  <linker>
    <assembly fullname="MyGame.Components" preserve="all" />
  </linker>
  ```
- **Scripts:** Jint is a pure managed interpreter (no IL emission). Script interop reaches your component types by
  reflection, so they need the same stripping protection. Leave `ScriptHostOptions.MemoryLimitBytes` off on IL2CPP
  (Jint polls `GC.GetAllocatedBytesForCurrentThread` per statement). Jint is pinned to the version
  `RoguelikeToolkit.World.Scripting.Jint` uses: a Unity project can only hold one.

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

Baseline (Apple M2, macOS Tahoe 26.3.1, .NET 10.0.12, BenchmarkDotNet 0.15.8;
spawn rows are per 100-entity batch, resolve rows per single cold resolve + 1 spawn):

| Benchmark | Mean | Allocated |
|---|---|---|
| `Load_200_Templates` | 11.54 ms | 5.02 MB |
| `BulkLoad_Then_QueryByTag` | 10.10 ms | 4.76 MB |
| `FolderLoad_200_Files` | 9.56 ms | 6.63 MB |
| `Resolve_Cold_SingleLevel` | 2.22 ms | 20.28 KB |
| `Resolve_Cold_TwoLevel` | 3.32 ms | 27.70 KB |
| `Spawn_Simple` | 6.84 ms | 1.30 MB |
| `Spawn_WithEmbedded` | 5.40 ms | 1.20 MB |
| `Spawn_ManyComponents` | 13.00 ms | 2.12 MB |

## Releases

Merging a PR into `main` computes the next version from conventional commits (GitVersion: `feat` minor, `fix`/others
patch, `!`/`BREAKING CHANGE` major), tags `vX.Y.Z`, writes the GitHub Release and publishes the NuGet package plus the
UPM tarball and plugins zip (`tools/upm/pack.py`, template in `upm/com.rogueliketoolkit.entities`) as release assets,
which OpenUPM picks up.

