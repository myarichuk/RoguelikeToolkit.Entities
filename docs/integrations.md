# Integration decisions (P7)

Rule: stdlib before external deps, packed format before mmap/native. Each item below records the
verdict, not just the option.

## ObjectTreeWalker / Fasterflect — removed (AOT)

Both generate IL at runtime (`Sigil` / `DynamicMethod`), which Unity IL2CPP and NativeAOT cannot execute. Member
mapping is now `ComponentMembers`: settable properties resolved once per type into a case-insensitive map, written
with plain `PropertyInfo.SetValue` (get-only auto-properties through their backing field). Components are set on
entities through one cached closed `ComponentAccessor<T>` per type (virtual call + unbox, no `MethodInfo.Invoke`).
Nested mappings recurse per level instead of walking a pre-built property tree. It turned out faster too
(spawn 6-40x, allocation 5-8x lower), because the old path paid the IL generation and invoke-array costs per spawn.
Semantics: properties only (any visibility), fields are reported as unmatched through `EntityDiagnostics`.

## deniszykov.TypeConversion — removed (6.0)

Its non-generic `Convert(Type, Type, object)` instantiates `GetConverter<TFrom, TTo>` via `MakeGenericMethod`, which
needs runtime generic instantiation over value types. It had already been demoted to a fallback behind the in-house
AOT-safe converter, and no test template reached the fallback (verified by instrumenting it). What it did on the
paths it still owned was wrong for game data: fractional -> integral truncated (`2.7` -> `2`) and out-of-range values
saturated (`1e20` -> `int.MaxValue`). `TemplateValueConverter` is now the only path: non-generic BCL calls,
range-checked, fractional numbers rejected for integral members, plus `Guid`/`TimeSpan`/`DateTime`/
`DateTimeOffset`/`Uri`/`Version` from strings and a public parser registry (`TemplateValueParsers`) for everything
else. One dependency (and `Microsoft.Extensions.Options`/`Primitives` with it) gone.

## RoguelikeToolkit.Dice — replaced by the built-in `Dice` (6.0)

ANTLR4 runtime + Troschuetz.Random for a grammar that fits in a few hundred lines of recursive descent. The old package
also rolled `1..sides-1` (exclusive upper bound: a d6 never rolled 6), shared one lexer/parser across threads, and
leaked an error listener per strict parse. The built-in parser compiles to a postfix program once; rolling is
allocation-free.

## Jint — split into `RoguelikeToolkit.Entities.Scripting.Jint` (6.0)

Scripting is optional; Jint + Acornima are ~3 MB and a version every Unity project has to agree on. The core package
now knows nothing about scripts: script types register their template parsers through `TemplateValueParsers`
(`EntityScripting.Register()`), and script interop uses the public `EntityComponentExtensions` (type-based component
access) instead of internals. Registration is explicit: module initializers / static constructors are fragile
under IL2CPP and trimming, and a missing registration fails loudly at the first spawn.

## RoguelikeToolkit.Scripts — replaced by `RoguelikeToolkit.Entities.Scripting.Jint`

The package (archived upstream, Jint 3 beta) created a Jint `Engine` per script instance, and the factory created a
script instance per spawned entity: ~53 µs and ~21 KB per entity per script member before anything ran. Extra arguments
went through `dynamic` (Microsoft.CSharp binder: runtime code generation, dead on IL2CPP), and its
`entity.GetComponent(Type)` interop could not bind from script at all (a Jint type reference is a JS function and converts
to a delegate, never to `Type`). Replacement: immutable scripts over a process-wide cache of prepared ASTs (shared across
entities, hosts and threads), one engine per caller-owned `ScriptHost`, `JsValue.FromObject` marshalling, and type
arguments unwrapped from `JsValue` and dispatched through the cached `ComponentAccessor<T>`. Construct: 52.8 µs / 21 KB ->
2.9 µs / 73 B (0 via the factory, which shares instances); execute with one argument: 15.8 µs -> 6.2 µs (net10, 10k
iterations). Jint 4.16.2 matches `RoguelikeToolkit.World.Scripting.Jint`.

Lua (MoonSharp: unmaintained; NLua: native library per platform) and C# scripting (Roslyn/CS-Script emit IL: impossible
under IL2CPP) were considered and rejected.

## SharpArena — deferred

Arena lists/stacks fit only short-lived scratch (BFS frontiers, parse tokens): they are not
thread-safe (one arena per thread), memory dies on `Reset`/`Dispose`, and templates/entities stay
managed regardless. The stdlib pool is already fixed (`using` + `Clear` on return), and spawning
rents nothing per spawn, so there is no measured GC pressure to remove. Revisit only with spawn-loop
profiling that shows buffer allocation as the bottleneck.

## Pools — internal (6.0)

`Microsoft.Extensions.ObjectPool` was used for four static pools of traversal buffers. One of them wrapped a custom
policy holding its own unbounded `ConcurrentQueue`, so it never actually bounded retention. Replaced by `Pool<T>`
(~60 lines): one fast slot plus a fixed slot array (2 x processors), each claimed by a single `CompareExchange`, no
locks, no allocation in steady state, overflow dropped for the GC. The dependency is gone.

## Memory-mapped / native JSON — last, and packed-first

Mmap does not fix 1M small files (the wall is inodes/enumeration, not read bandwidth). The required
order is: pack blueprints into a single file/DB first, then read it with `Utf8JsonReader` over a
`MemoryMappedFile` + source-generated `System.Text.Json`, with explicit lifetime and hot-reload
invalidation. Until the packed format exists, mmap work is premature.
