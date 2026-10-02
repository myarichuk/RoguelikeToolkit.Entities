using BenchmarkDotNet.Attributes;
using RoguelikeToolkit.Entities.Repository;

namespace RoguelikeToolkit.Entities.Benchmarks;

/// <summary>
/// Folder-load benchmarks: templates on disk (the mod / content-pack path), parsed in
/// parallel and committed atomically. Complements <c>LoadBenchmarks</c>, which loads the
/// same shapes from in-memory streams and therefore skips file IO and enumeration.
/// </summary>
[MemoryDiagnoser]
public class FolderLoadBenchmarks
{
    private const int TemplateCount = 200;

    private DirectoryInfo _folder = null!;
    private EntityTemplateRepository _repository = new();

    /// <summary>Writes the template pack to a temp dir once.</summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _folder = Directory.CreateTempSubdirectory("bench-templates-");
        for (var i = 0; i < TemplateCount; i++)
        {
            File.WriteAllText(
                Path.Combine(_folder.FullName, $"bench-folder-{i}.yaml"),
                $"""
                Tags:
                 - mob
                 - tier-{i % 5}
                Components:
                 BenchHealth:
                  Max: {10 + i}
                  Current: {10 + i}
                 BenchSpeed:
                  Value: {i % 9}
                """);
        }
    }

    /// <summary>Removes the template pack.</summary>
    [GlobalCleanup]
    public void GlobalCleanup() =>
        _folder.Delete(recursive: true);

    /// <summary>Starts each iteration with an empty repository.</summary>
    [IterationSetup]
    public void IterationSetup() =>
        _repository = new EntityTemplateRepository();

    /// <summary>Loads the whole on-disk pack into a fresh repository.</summary>
    /// <returns>Number of templates loaded.</returns>
    [Benchmark]
    public int FolderLoad_200_Files()
    {
        _repository.LoadTemplateFolder(_folder.FullName);
        return _repository.TemplateNames.Count();
    }
}
