# Integration decisions (P7)

Rule: stdlib before external deps, packed format before mmap/native. Each item below records the
verdict, not just the option.

## ObjectTreeWalker — adopted

`ComponentFactory` maps template dictionaries onto component members through `ObjectMemberIterator`
(cached accessors + traversal) instead of the old hand-rolled `PropertyInfo` loop (`O(M*P)` scan,
`Last()` in the loop, per-call `GetInterfaces`). The hand-rolled path (`TrySetPropertyValue` /
`TryGetDestPropertyFor` / `_typePropertyCache`) was dead code and has been removed, so there is exactly
one member-mapping path. Semantics to remember: properties only — fields are ignored and reported
through `EntityDiagnostics.WarningHandler`, same as any other unmatched template property.

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
