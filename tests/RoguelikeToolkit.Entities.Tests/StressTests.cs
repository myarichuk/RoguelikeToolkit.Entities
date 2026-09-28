using System.Text;
using DefaultEcs;
using RoguelikeToolkit.Entities.Factory;
using RoguelikeToolkit.Entities.Repository;

#pragma warning disable CS1591
namespace RoguelikeToolkit.Entities.Tests;

/// <summary>
/// Mass-data / high-frequency stress tests: 2k spawns per blueprint, bulk loads,
/// and concurrent reads. These guard the scale goals, not exact timings.
/// </summary>
public class StressTests
{
    private const string SpawnYaml = """
        Tags:
         - stress
        Components:
         Foobar:
          NumProperty: 1
          StringProperty: x
         Barfoo:
          AnotherNumProperty: 2
          AnotherStringProperty: y
        """;

    private const string BaseYaml = """
        Tags:
         - stress-base
        Components:
         Foobar:
          NumProperty: 7
          StringProperty: base
        """;

    private const string DerivedYaml = """
        Inherits:
         - stress-base
        Tags:
         - stress-derived
        Components:
         Barfoo:
          AnotherNumProperty: 8
          AnotherStringProperty: derived
        """;

    [Fact]
    public void Can_spawn_2000_instances_of_one_blueprint()
    {
        var repository = new EntityTemplateRepository();
        LoadFromString(repository, "stress-blueprint", SpawnYaml);

        using var world = new World();
        var factory = new EntityFactory(repository, world);

        const int instanceCount = 2000;
        for (var i = 0; i < instanceCount; i++)
        {
            Assert.True(factory.TryCreate("stress-blueprint", out _));
        }

        Assert.Equal(instanceCount, world.GetEntities().AsSet().Count);
    }

    [Fact]
    public void Can_load_1500_templates_and_query_them()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"stress-templates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            const int templateCount = 1500;
            for (var i = 0; i < templateCount; i++)
            {
                File.WriteAllText(
                    Path.Combine(folder, $"stress-{i}.yaml"),
                    $"""
                     Tags:
                      - stress-all
                      - {(i % 2 == 0 ? "even" : "odd")}
                     Components:
                      Foobar:
                       NumProperty: {i}
                       StringProperty: s{i}
                     """);
            }

            var repository = new EntityTemplateRepository();
            repository.LoadTemplateFolder(folder);

            Assert.Equal(templateCount, repository.TemplateNames.Count());
            Assert.Equal(templateCount / 2, repository.GetByTags("even").Count());
            Assert.Equal(templateCount, repository.GetByTags("stress-all").Count());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Concurrent_repository_reads_are_safe()
    {
        var repository = new EntityTemplateRepository();
        LoadFromString(repository, "stress-base", BaseYaml);
        LoadFromString(repository, "stress-derived", DerivedYaml);

        const int readers = 64;
        var hits = new int[readers];
        Parallel.For(
            0,
            readers,
            i =>
            {
                hits[i] = repository.TryGetByName("stress-derived", out _) &&
                    repository.GetByTags("stress-base").Count() == 1 &&
                    repository.GetByTags("stress-derived").Count() == 1
                    ? 1
                    : 0;
            });

        Assert.All(hits, static h => Assert.Equal(1, h));
    }

    [Fact]
    public void Concurrent_effective_template_resolution_is_safe()
    {
        var repository = new EntityTemplateRepository();
        LoadFromString(repository, "stress-base", BaseYaml);
        LoadFromString(repository, "stress-derived", DerivedYaml);

        Assert.True(repository.TryGetByName("stress-derived", out var template));
        var resolver = new EntityInheritanceResolver(repository.TryGetByName);
        var expected = resolver.GetEffectiveTemplate(template);

        const int readers = 64;
        var results = new EntityTemplate[readers];
        Parallel.For(0, readers, i => results[i] = resolver.GetEffectiveTemplate(template));

        Assert.All(results, result => Assert.Same(expected, result));
    }

    private static void LoadFromString(EntityTemplateRepository repository, string name, string yaml)
    {
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(yaml)), Encoding.UTF8);
        repository.LoadTemplate(name, reader);
    }
}
