using RoguelikeToolkit.Entities.Factory;
using RoguelikeToolkit.Entities.Repository;

#pragma warning disable CS1591
namespace RoguelikeToolkit.Entities.Tests;

public class EffectiveTemplateCacheTests
{
    [Fact]
    public void Canonical_template_is_resolved_once_and_shared()
    {
        var repository = new EntityTemplateRepository();
        repository.LoadTemplateFolder("TemplatesForInheritanceResolver");
        var resolver = new EntityInheritanceResolver(repository.TryGetByName);

        Assert.True(repository.TryGetByName("template-no-inheritance", out var template));

        var first = resolver.GetEffectiveTemplate(template);
        var second = resolver.GetEffectiveTemplate(template);

        Assert.Same(first, second);
    }

    [Fact]
    public void Unnamed_template_is_cached_by_reference()
    {
        var resolver = new EntityInheritanceResolver((string _, out EntityTemplate found) =>
        {
            found = null!;
            return false;
        });

        var template = new EntityTemplate();

        Assert.Same(resolver.GetEffectiveTemplate(template), resolver.GetEffectiveTemplate(template));
    }

    [Fact]
    public void Failures_are_not_cached()
    {
        var baseTemplate = new EntityTemplate { Name = "base" };
        var allowBase = false;
        var resolver = new EntityInheritanceResolver((string name, out EntityTemplate found) =>
        {
            if (allowBase && name == "base")
            {
                found = baseTemplate;
                return true;
            }

            found = null!;
            return false;
        });

        var derived = new EntityTemplate { Name = "derived" };
        derived.AddInherit("base");

        Assert.Throws<InvalidOperationException>(() => resolver.GetEffectiveTemplate(derived));

        allowBase = true;
        var effective = resolver.GetEffectiveTemplate(derived);

        Assert.NotNull(effective);
        Assert.Same(effective, resolver.GetEffectiveTemplate(derived));
    }

    [Fact]
    public void Same_named_embedded_templates_do_not_collide()
    {
        var baseA = new EntityTemplate { Name = "base-a" };
        baseA.AddTag("tag-a");
        var baseB = new EntityTemplate { Name = "base-b" };
        baseB.AddTag("tag-b");

        var resolver = new EntityInheritanceResolver((string name, out EntityTemplate found) =>
        {
            found = name switch
            {
                "base-a" => baseA,
                "base-b" => baseB,
                _ => null!,
            };
            return found != null;
        });

        // Two embedded templates that share a local name but inherit different bases.
        var childA = new EntityTemplate { Name = "child" };
        childA.AddInherit("base-a");
        var childB = new EntityTemplate { Name = "child" };
        childB.AddInherit("base-b");

        var effectiveA = resolver.GetEffectiveTemplate(childA);
        var effectiveB = resolver.GetEffectiveTemplate(childB);

        Assert.NotSame(effectiveA, effectiveB);
        Assert.Contains("tag-a", effectiveA.Tags);
        Assert.Contains("tag-b", effectiveB.Tags);
    }

    [Fact]
    public void InvalidateCache_forces_re_resolution()
    {
        var repository = new EntityTemplateRepository();
        repository.LoadTemplateFolder("TemplatesForInheritanceResolver");
        var resolver = new EntityInheritanceResolver(repository.TryGetByName);

        Assert.True(repository.TryGetByName("template-no-inheritance", out var template));

        var first = resolver.GetEffectiveTemplate(template);
        resolver.InvalidateCache();
        var second = resolver.GetEffectiveTemplate(template);

        Assert.NotSame(first, second);
        Assert.Equal(first.Tags, second.Tags);
    }
}

public class DiagnosticsHookTests : IDisposable
{
    private readonly List<string> _warnings = new();
    private readonly ComponentFactory _componentFactory = new();

    public DiagnosticsHookTests() =>
        EntityDiagnostics.WarningHandler = _warnings.Add;

    public void Dispose() =>
        EntityDiagnostics.WarningHandler = null;

    [Fact]
    public void Unknown_template_property_warns_but_still_creates()
    {
        var objectData = new Dictionary<object, object>
        {
            ["NumProperty"] = 123,
            ["StringProperty"] = "abcdef",
            ["NoSuchProperty"] = "oops",
        };

        Assert.True(_componentFactory.TryCreateReferenceInstance<ComponentFactoryTests.Foobar>(objectData, out var instance));

        Assert.Equal(123, instance.NumProperty);
        Assert.Equal("abcdef", instance.StringProperty);
        Assert.Contains(_warnings, w => w.Contains("NoSuchProperty"));
    }

    [Fact]
    public void Throwing_handler_does_not_break_creation()
    {
        EntityDiagnostics.WarningHandler = _ => throw new InvalidOperationException("logging is down");

        var objectData = new Dictionary<object, object>
        {
            ["NumProperty"] = 7,
            ["NoSuchProperty"] = "oops",
        };

        Assert.True(_componentFactory.TryCreateReferenceInstance<ComponentFactoryTests.Foobar>(objectData, out var instance));
        Assert.Equal(7, instance.NumProperty);
    }

    [Fact]
    public void Matched_subset_produces_no_warnings_for_matched_keys()
    {
        var objectData = new Dictionary<object, object>
        {
            ["StringProperty"] = "abcdef",
        };

        Assert.True(_componentFactory.TryCreateReferenceInstance<ComponentFactoryTests.PartialFoobar>(objectData, out _));
        Assert.Empty(_warnings);
    }
}
