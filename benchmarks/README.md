# Benchmarks

Allocation + time benchmarks (BenchmarkDotNet) for the spawn and load paths. Run them in
Release — Debug numbers are meaningless:

```bash
dotnet run --project benchmarks/RoguelikeToolkit.Entities.Benchmarks -c Release
```

Useful filters:

```bash
# spawn path only, quick smoke run
dotnet run --project benchmarks/RoguelikeToolkit.Entities.Benchmarks -c Release -- --filter *Spawn* --job short
# load path only
dotnet run --project benchmarks/RoguelikeToolkit.Entities.Benchmarks -c Release -- --filter *Load*
```

## What is measured

- `Spawn_Simple` — 100-entity batch, one inherited template (steady state: inheritance
  resolves once per factory, the rest hits the effective-template cache).
- `Spawn_WithEmbedded` — parent + 2 children per spawn (child-creation cost, no duplication).
- `Spawn_ManyComponents` — 5 components per entity (member-mapping cost).
- `Load_200_Templates` — parse + index cost for 200 templates (in-memory streams) into a fresh repository.
- `BulkLoad_Then_QueryByTag` — bulk load plus one tag-index query.
- `FolderLoad_200_Files` — the on-disk content-pack path: enumerate + parallel-parse + commit 200 files.
- `Resolve_Cold_SingleLevel` / `Resolve_Cold_TwoLevel` — cold inheritance resolution (fresh
  factory, empty effective-template cache) for 1- and 2-level `Inherits` chains, then one spawn.
  Warm (cached) resolution is covered by the `Spawn_*` steady-state batches.

Every benchmark reports allocated bytes (`MemoryDiagnoser`). Compare against the checked-in
baseline below when changing the spawn/load path; investigate any allocation increase per
entity or per template before merging.

## Baseline

Apple M2, macOS Tahoe 26.3.1, .NET 10.0.12, BenchmarkDotNet 0.15.8. Spawn means are per
100-entity batch; resolve means are per single cold resolve + 1 spawn.

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
