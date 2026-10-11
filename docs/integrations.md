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

## deniszykov.TypeConversion — fallback only

Its non-generic `Convert(Type, Type, object)` instantiates `GetConverter<TFrom, TTo>` via `MakeGenericMethod`, which
needs runtime generic instantiation over value types. `AotValueConverter` handles what templates contain (assignable
values, numbers, strings, bools, enums, dice/script strings) with non-generic BCL calls first; the provider only sees
exotic conversions.

## RoguelikeToolkit.Scripts — replaced by `RoguelikeToolkit.Entities.Scripting`

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

## Pools — stdlib first (done)

All pooling goes through `Microsoft.Extensions.ObjectPool` (`DefaultObjectPoolProvider` via
`ObjectPoolProvider`; one custom `ThreadSafeObjectPoolPolicy` for entity buffers). No custom
pool framework.

## Memory-mapped / native JSON — last, and packed-first

Mmap does not fix 1M small files (the wall is inodes/enumeration, not read bandwidth). The required
order is: pack blueprints into a single file/DB first, then read it with `Utf8JsonReader` over a
`MemoryMappedFile` + source-generated `System.Text.Json`, with explicit lifetime and hot-reload
invalidation. Until the packed format exists, mmap work is premature.
