using BenchmarkDotNet.Attributes;
using DefaultEcs;
using RoguelikeToolkit.Entities.Factory;
using RoguelikeToolkit.Entities.Repository;

namespace RoguelikeToolkit.Entities.Benchmarks;

/// <summary>
/// Cold inheritance-resolution benchmarks. Each iteration uses a fresh factory (empty
/// effective-template cache), so the measured call walks the <c>Inherits</c> chain, merges
/// and caches — then spawns a single entity. Warm (cached) resolution is already covered
/// by <c>SpawnBenchmarks</c> steady-state batches.
/// </summary>
[MemoryDiagnoser]
public class ResolveBenchmarks
{
    private const string BaseYaml = """
        Tags:
         - mob
        Components:
         BenchHealth:
          Max: 10
          Current: 10
        """;

    private const string MidYaml = """
        Inherits:
         - bench-resolve-base
        Components:
         BenchSpeed:
          Value: 5
        """;

    private const string LeafDirectYaml = """
        Inherits:
         - bench-resolve-base
        Tags:
         - goblin
        Components:
         BenchArmor:
          Value: 4
        """;

    private const string LeafYaml = """
        Inherits:
         - bench-resolve-mid
        Tags:
         - elite
        Components:
         BenchArmor:
          Value: 8
        """;

    private EntityTemplateRepository _repository = new();
    private World _world = new();
    private EntityFactory _factory = null!;

    /// <summary>Loads the bench templates once.</summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _repository = new EntityTemplateRepository();
        Load("bench-resolve-base", BaseYaml);
        Load("bench-resolve-mid", MidYaml);
        Load("bench-resolve-leaf-direct", LeafDirectYaml);
        Load("bench-resolve-leaf", LeafYaml);
    }

    /// <summary>Recreates the world + factory so every iteration resolves cold.</summary>
    [IterationSetup]
    public void IterationSetup()
    {
        _world = new World();
        _factory = new EntityFactory(_repository, _world);
    }

    /// <summary>Disposes the single-entity world so memory cannot leak across iterations.</summary>
    [IterationCleanup]
    public void IterationCleanup() =>
        _world.Dispose();

    /// <summary>Cold-resolves a one-level inheritance chain, then spawns one entity.</summary>
    /// <returns>1 when the entity was created, 0 otherwise.</returns>
    [Benchmark]
    public int Resolve_Cold_SingleLevel() =>
        _factory.TryCreate("bench-resolve-leaf-direct", out _) ? 1 : 0;

    /// <summary>Cold-resolves a two-level inheritance chain, then spawns one entity.</summary>
    /// <returns>1 when the entity was created, 0 otherwise.</returns>
    [Benchmark]
    public int Resolve_Cold_TwoLevel() =>
        _factory.TryCreate("bench-resolve-leaf", out _) ? 1 : 0;

    private void Load(string name, string yaml)
    {
        using var reader = ReaderFor(yaml);
        _repository.LoadTemplate(name, reader);
    }

    private static StreamReader ReaderFor(string yaml) =>
        new(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml)), System.Text.Encoding.UTF8);
}
