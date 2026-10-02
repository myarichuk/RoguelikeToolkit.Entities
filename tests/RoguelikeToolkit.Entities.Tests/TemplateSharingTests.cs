using RoguelikeToolkit.Entities.Exceptions;
using RoguelikeToolkit.Entities.Factory;
using RoguelikeToolkit.Entities.Repository;

#pragma warning disable CS1591
namespace RoguelikeToolkit.Entities.Tests;

[Collection("SequentialWarningHook")]
public class TemplateSharingTests : IDisposable
{
    private readonly List<string> _warnings = new();
    private readonly EntityTemplateRepository _repository = new();

    public TemplateSharingTests() =>
        EntityDiagnostics.WarningHandler = _warnings.Add;

    public void Dispose() =>
        EntityDiagnostics.WarningHandler = null;

    [Fact]
    public void Loaded_template_is_shared_and_mutation_warns_but_applies()
    {
        _repository.LoadTemplate("base", "Components:\n foobar:\n  numProperty: 1\n");

        Assert.True(_repository.TryGetByName("base", out var shared));
        Assert.True(shared.IsShared);

        shared.AddTag("late");

        Assert.Contains(_warnings, w => w.Contains("shared") && w.Contains("Copy()"));
        Assert.Contains("late", shared.Tags);
    }

    [Fact]
    public void Copy_is_unshared_and_diverges_silently()
    {
        _repository.LoadTemplate("base", "Components:\n foobar:\n  numProperty: 1\n");
        Assert.True(_repository.TryGetByName("base", out var shared));

        var variant = shared.Copy();

        Assert.False(variant.IsShared);
        variant.AddTag("brute");
        variant.Name = "brute";

        Assert.Empty(_warnings);
        Assert.DoesNotContain("brute", shared.Tags);
        Assert.Equal("base", shared.Name);
        Assert.Equal("brute", variant.Name);
    }

    [Fact]
    public void AddTemplate_stores_defensive_copy()
    {
        var original = new EntityTemplate { Name = "mob" };
        original.AddTag("mob-tag");

        _repository.AddTemplate("mob", original);
        original.AddTag("after-add");

        Assert.True(_repository.TryGetByName("mob", out var stored));
        Assert.True(stored.IsShared);
        Assert.DoesNotContain("after-add", stored.Tags);
        Assert.Contains("mob-tag", stored.Tags);
    }

    [Fact]
    public void RemoveTemplate_clears_repository_and_tag_index()
    {
        _repository.LoadTemplate("tagged", "Tags:\n - squad\nComponents:\n foo: bar\n");

        Assert.NotEmpty(_repository.GetByTags("squad"));
        Assert.True(_repository.RemoveTemplate("tagged"));
        Assert.False(_repository.RemoveTemplate("tagged"));

        Assert.False(_repository.TryGetByName("tagged", out _));
        Assert.Empty(_repository.GetByTags("squad"));
    }

    [Fact]
    public void LoadTemplate_from_string_content()
    {
        _repository.LoadTemplate("from-string", "Tags:\n - t1\nComponents:\n foo: bar\n");

        Assert.True(_repository.TryGetByName("from-string", out var template));
        Assert.Contains("t1", template.Tags);
    }

    [Fact]
    public void Effective_template_is_shared()
    {
        _repository.LoadTemplate("base", "Components:\n foo: bar\n");
        Assert.True(_repository.TryGetByName("base", out var template));

        var resolver = new EntityInheritanceResolver(_repository.TryGetByName);
        var effective = resolver.GetEffectiveTemplate(template);

        Assert.True(effective.IsShared);
        Assert.Same(effective, resolver.GetEffectiveTemplate(template));
    }

    [Fact]
    public void Duplicate_ref_target_fails_instead_of_silently_dropping()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dup-ref-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "a.yaml"), "Components:\n foo: bar\n");
            File.WriteAllText(
                Path.Combine(folder, "main.yaml"),
                "$ref: a.yaml\na.yaml:\n  Components:\n    foo: baz\n");

            var loader = new EntityTemplateLoader();
            var exception = Assert.Throws<FailedToParseException>(() =>
                loader.LoadFrom(new FileInfo(Path.Combine(folder, "main.yaml"))));
            Assert.Contains("Duplicate embedded template name", exception.Message);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
