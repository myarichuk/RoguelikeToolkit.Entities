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
- `Load_200_Templates` — parse + index cost for 200 templates into a fresh repository.
- `BulkLoad_Then_QueryByTag` — bulk load plus one tag-index query.

Every benchmark reports allocated bytes (`MemoryDiagnoser`). Compare against the checked-in
baseline below when changing the spawn/load path; investigate any allocation increase per
entity or per template before merging.

## Baseline (add yours here)

| Benchmark | Mean | Allocated |
|---|---|---|
| _run on your machine, paste_ | | |

Record: CPU, OS, .NET version, BenchmarkDotNet version.
