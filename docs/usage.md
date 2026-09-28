# Usage: querying, spawning, pooling, save/load, modding

## Querying templates

```csharp
repository.TryGetByName("goblin", out var template); // O(1), case-insensitive
var fighters = repository.GetByTags("enemy", "melee"); // inverted index: cost follows matches, not repo size
var all = repository.GetByTags(); // empty query returns everything
```

`GetByTags` requires ALL tags and is case-insensitive. The index reflects tags as of load;
mutating a loaded template afterwards does not update it (call
`EntityFactory.InvalidateEffectiveTemplateCache()` and prefer re-loading for mod content).

Live entities use `EntityExtension`: `entity.HasTags("enemy")`,
`parent.GetChildrenWithTags("enemy")`.

## Spawning

```csharp
var factory = new EntityFactory(repository, world);
if (factory.TryCreate("goblin", out var entity)) { /* ... */ }
```

Inheritance is resolved once per template and cached (shared read-only result); children spawn by
recursing into direct children and are linked with `SetAsParentOf`. Components are built per spawn:
dice strings share one parse per distinct expression, scripts are constructed per entity (they hold
per-entity state and are never shared). Global components are first-write-wins: the first spawned
entity sets the world's copy, later spawns link to it.

Template typos surface through `EntityDiagnostics.WarningHandler`: a template property that matches
no component member (renamed property, field instead of property) warns instead of silently dropping.
Hard failures (unknown component type, null value, cycles) throw with template + member context.

## Pooling

Entity recycling is DefaultEcs' job (`World`). The library only pools short-lived traversal buffers
(`Queue`/`Stack` in `EmbeddedTemplateGraphIterator`, entity sets in `EntityExtensions`) through
`Microsoft.Extensions.ObjectPool` (stdlib `DefaultObjectPoolProvider`). Pooled containers are cleared
on return and iterators are single-threaded; spawning itself rents nothing per spawn.

## Save/load

There is no built-in save format. The recommended pattern: persist the template name plus dynamic
component state, then load by re-spawning and re-applying state:

```csharp
// save
Save(enemyTemplateName, entity.Get<Health>().Value, entity.Get<Position>());
// load
factory.TryCreate(savedTemplateName, out var entity);
entity.Set(new Health { Value = savedHp });
```

## Modding / hot-reload

Loading is additive: `LoadTemplate` / `LoadTemplateFolder` reject duplicate names with
`TemplateAlreadyExistsException`. Folder loads are atomic — parsing runs in parallel off to the
side, and on any failure an `AggregateException` (one inner exception per file) is thrown with the
repository left unchanged:

```csharp
await repository.LoadTemplateFolderAsync("Mods/Extra", cancellationToken);
```

There is no unload: to hot-reload a mod, load it into a fresh `EntityTemplateRepository`
(+ `EntityFactory`) and drop the old pair. If you mutate an already-loaded template in place,
call `factory.InvalidateEffectiveTemplateCache()`; plain loading never needs it.

## Template format (YAML vs JSON)

Write YAML for authoring (comments, anchors, readability), ship JSON (or a packed single file) for
spawn-time loading — the same loader reads both (`.yaml`/`.json`, extension case-insensitive).
Only the last extension is stripped, so `boss.final.yaml` loads as `boss.final`. Relative
`$ref` / `$merge-ref` paths resolve against the referencing file's directory.

```yaml
Components:
  foobar:
    stringProperty: abcdef
    numProperty: 123
Tags: [enemy, melee]
Inherits: [base-fighter]
sword:
  Components:
    foobar:
      stringProperty: sharp
```
