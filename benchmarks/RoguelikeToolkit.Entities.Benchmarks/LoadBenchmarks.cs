using System.Text;
using BenchmarkDotNet.Attributes;
using RoguelikeToolkit.Entities.Repository;

namespace RoguelikeToolkit.Entities.Benchmarks;

/// <summary>
/// Load-path benchmarks: time + allocations for parsing templates into the repository.
/// </summary>
[MemoryDiagnoser]
public class LoadBenchmarks
{
    private const int TemplateCount = 200;

    private readonly List<(string Name, string Yaml)> _templates = new(TemplateCount);
    private EntityTemplateRepository _repository = new();

    /// <summary>Generates the template payloads once.</summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _templates.Clear();
        for (var i = 0; i < TemplateCount; i++)
        {
            _templates.Add(($"bench-load-{i}", $"""
                Tags:
                 - mob
                 - tier-{i % 5}
                Components:
                 BenchHealth:
                  Max: {10 + i}
                  Current: {10 + i}
                 BenchSpeed:
                  Value: {i % 9}
                """));
        }
    }

    /// <summary>Starts each iteration with an empty repository.</summary>
    [IterationSetup]
    public void IterationSetup() =>
        _repository = new EntityTemplateRepository();

    /// <summary>Loads every generated template into a fresh repository.</summary>
    /// <returns>Number of templates loaded.</returns>
    [Benchmark]
    public int Load_200_Templates()
    {
        foreach (var (name, yaml) in _templates)
        {
            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(yaml)), Encoding.UTF8);
            _repository.LoadTemplate(name, reader);
        }

        return _templates.Count;
    }

    /// <summary>Bulk-loads all templates and then queries the tag index (index use, not a full scan).</summary>
    /// <returns>Number of templates matching the tag.</returns>
    [Benchmark]
    public int BulkLoad_Then_QueryByTag()
    {
        foreach (var (name, yaml) in _templates)
        {
            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(yaml)), Encoding.UTF8);
            _repository.LoadTemplate(name, reader);
        }

        return _repository.GetByTags("mob").Count();
    }
}
