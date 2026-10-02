using DefaultEcs;
using RoguelikeToolkit.Entities.Factory;
using RoguelikeToolkit.Entities.Repository;

#pragma warning disable CS1591
namespace RoguelikeToolkit.Entities.Tests;

[CollectionDefinition("SequentialWarningHook", DisableParallelization = true)]
public class WarningHookCollection
{
}

[Collection("SequentialWarningHook")]
public class SpawnDiagnosticsTests : IDisposable
{
    private readonly List<string> _warnings = new();

    public SpawnDiagnosticsTests() =>
        EntityDiagnostics.WarningHandler = _warnings.Add;

    public void Dispose() =>
        EntityDiagnostics.WarningHandler = null;

    [Fact]
    public void Ignored_global_rewrite_warns()
    {
        var repository = new EntityTemplateRepository();
        repository.LoadTemplate("global-first", "Components:\n attributes:\n  strength: 12\n  agility: 8\n");
        repository.LoadTemplate("global-second", "Components:\n attributes:\n  strength: 99\n  agility: 1\n");

        using var world = new World();
        var factory = new EntityFactory(repository, world);

        Assert.True(factory.TryCreate("global-first", out _));
        Assert.True(factory.TryCreate("global-second", out _));

        Assert.Contains(_warnings, w => w.Contains("first write wins"));
    }

    [Fact]
    public void Unknown_component_type_throws_with_template_context()
    {
        var repository = new EntityTemplateRepository();
        repository.LoadTemplate("bad-template", "Components:\n nosuchcomponent:\n  x: 1\n");

        using var world = new World();
        var factory = new EntityFactory(repository, world);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.TryCreate("bad-template", out _));
        Assert.Contains("nosuchcomponent", exception.Message);
    }

    [Fact]
    public void Case_duplicate_property_warns_and_keeps_first()
    {
        var componentFactory = new ComponentFactory();
        var objectData = new Dictionary<object, object>
        {
            ["NumProperty"] = 1,
            ["numproperty"] = 2,
            ["StringProperty"] = "abcdef",
        };

        Assert.True(componentFactory.TryCreateReferenceInstance<ComponentFactoryTests.Foobar>(objectData, out var instance));

        Assert.Equal(1, instance.NumProperty);
        Assert.Equal("abcdef", instance.StringProperty);
        Assert.Contains(_warnings, w => w.Contains("duplicate") && w.Contains("numproperty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Inconvertible_value_component_throws()
    {
        var componentFactory = new ComponentFactory();

        Assert.ThrowsAny<Exception>(() =>
            componentFactory.TryCreateValueInstance(typeof(AnotherFoobar), "not-a-number", out _));
    }
}
