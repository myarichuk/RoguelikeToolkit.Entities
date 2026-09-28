using System.Text;
using BenchmarkDotNet.Attributes;
using DefaultEcs;
using RoguelikeToolkit.Entities.Factory;
using RoguelikeToolkit.Entities.Repository;

namespace RoguelikeToolkit.Entities.Benchmarks;

/// <summary>
/// Spawn-path benchmarks: time + allocations per entity. Each invocation spawns a batch into a
/// fresh <see cref="World"/> (recreated per iteration) so worlds cannot grow unboundedly across
/// iterations. The repository is shared; the factory is recreated per iteration, so the first
/// spawn of a batch resolves inheritance once and the rest measure the cached steady state.
/// </summary>
[MemoryDiagnoser]
public class SpawnBenchmarks
{
    private const int SpawnsPerInvoke = 100;

    private const string BaseYaml = """
        Tags:
         - mob
        Components:
         BenchHealth:
          Max: 10
          Current: 10
         BenchSpeed:
          Value: 5
        """;

    private const string SimpleYaml = """
        Inherits:
         - bench-base
        Tags:
         - goblin
        Components:
         BenchAttack:
          Damage: 1d6
          Range: 2
        """;

    private const string EmbeddedYaml = """
        Components:
         BenchHealth:
          Max: 30
          Current: 30
        child-a:
         Components:
          BenchSpeed:
           Value: 7
        child-b:
         Components:
          BenchSpeed:
           Value: 9
        """;

    private const string ManyComponentsYaml = """
        Inherits:
         - bench-base
        Components:
         BenchAttack:
          Damage: 2d6+3
          Range: 3
         BenchArmor:
          Value: 4
         BenchName:
          Text: elite
        """;

    private EntityTemplateRepository _repository = new();
    private World _world = new();
    private EntityFactory _factory = null!;

    /// <summary>Loads the bench templates once.</summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _repository = new EntityTemplateRepository();
        Load("bench-base", BaseYaml);
        Load("bench-simple", SimpleYaml);
        Load("bench-embedded", EmbeddedYaml);
        Load("bench-many", ManyComponentsYaml);
    }

    /// <summary>Recreates the world + factory so each iteration measures a bounded batch.</summary>
    [IterationSetup]
    public void IterationSetup()
    {
        _world = new World();
        _factory = new EntityFactory(_repository, _world);
    }

    /// <summary>Disposes the batch world so memory cannot leak across iterations.</summary>
    [IterationCleanup]
    public void IterationCleanup() =>
        _world.Dispose();

    /// <summary>Spawns a batch of entities with one inherited template.</summary>
    /// <returns>Number of entities spawned.</returns>
    [Benchmark]
    public int Spawn_Simple()
    {
        var spawned = 0;
        for (var i = 0; i < SpawnsPerInvoke; i++)
        {
            if (_factory.TryCreate("bench-simple", out _))
            {
                spawned++;
            }
        }

        return spawned;
    }

    /// <summary>Spawns a batch of entities with embedded children (parent + 2 children each).</summary>
    /// <returns>Number of root entities spawned.</returns>
    [Benchmark]
    public int Spawn_WithEmbedded()
    {
        var spawned = 0;
        for (var i = 0; i < SpawnsPerInvoke; i++)
        {
            if (_factory.TryCreate("bench-embedded", out _))
            {
                spawned++;
            }
        }

        return spawned;
    }

    /// <summary>Spawns a batch of entities carrying five components each.</summary>
    /// <returns>Number of entities spawned.</returns>
    [Benchmark]
    public int Spawn_ManyComponents()
    {
        var spawned = 0;
        for (var i = 0; i < SpawnsPerInvoke; i++)
        {
            if (_factory.TryCreate("bench-many", out _))
            {
                spawned++;
            }
        }

        return spawned;
    }

    private void Load(string name, string yaml)
    {
        using var reader = ReaderFor(yaml);
        _repository.LoadTemplate(name, reader);
    }

    private static StreamReader ReaderFor(string yaml) =>
        new(new MemoryStream(Encoding.UTF8.GetBytes(yaml)), Encoding.UTF8);
}
