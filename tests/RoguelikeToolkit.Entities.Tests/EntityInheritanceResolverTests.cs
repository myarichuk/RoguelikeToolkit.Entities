using RoguelikeToolkit.Entities.Factory;
using RoguelikeToolkit.Entities.Repository;
// ReSharper disable MethodTooLong
// ReSharper disable StringLiteralTypo
#pragma warning disable CS1591

// ReSharper disable ExceptionNotDocumented
namespace RoguelikeToolkit.Entities.Tests
{
    public class EntityInheritanceResolverTests
    {
        private readonly EntityTemplateRepository _repository = new();
        private readonly EntityInheritanceResolver _inheritanceResolver;

        public EntityInheritanceResolverTests()
        {
            _repository.LoadTemplateFolder("TemplatesForInheritanceResolver");
            _inheritanceResolver = new(_repository.TryGetByName);
        }

        [Fact]
        public void Should_throw_on_non_existing_inherit_entries()
        {
            // sanity check
            Assert.True(_repository.TryGetByName("inherited-template-non-existing-inherit", out var template));
            Assert.Throws<InvalidOperationException>(() => _inheritanceResolver.GetEffectiveTemplate(template));
        }

        [Fact]
        public void Can_resolve_without_inheritance()
        {
            // sanity check
            Assert.True(_repository.TryGetByName("template-no-inheritance", out var template));
            var templateWithInheritanceResolved = _inheritanceResolver.GetEffectiveTemplate(template);

            Assert.Collection(
                templateWithInheritanceResolved.Tags,
                item => Assert.Equal("tag1", item),
                item => Assert.Equal("tag2", item),
                item => Assert.Equal("tag3", item));

            Assert.Collection(
                templateWithInheritanceResolved.Components,
                kvp =>
                {
                    Assert.Equal("foobar", kvp.Key);

                    // embedded objects yaml deserializer loads as Dictionary<object, object>
                    var valueAsDict = (Dictionary<object, object>)kvp.Value;
                    Assert.Equal("abcdef", valueAsDict["stringProperty"]);
                    Assert.Equal((byte)123, valueAsDict["numProperty"]);
                },
                kvp =>
                {
                    Assert.Equal("barfoo", kvp.Key);
                    var valueAsDict = (Dictionary<object, object>)kvp.Value;
                    Assert.Equal("defgh", valueAsDict["anotherStringProperty"]);
                    Assert.Equal((byte)234, valueAsDict["anotherNumProperty"]);
                },
                kvp =>
                {
                    // yaml deserializer loads simple objects as key-value pairs
                    Assert.Equal("foo", kvp.Key);
                    Assert.Equal("this is a test!", kvp.Value);
                });
        }

        [Fact]
        public void Merges_embedded_templates_from_base()
        {
            var child = new EntityTemplate { Name = "child" };
            var baseTemplate = new EntityTemplate { Name = "base" };
            baseTemplate.MergeEmbeddedTemplates(new HashSet<EntityTemplate> { child });

            var derived = new EntityTemplate { Name = "derived" };
            derived.Inherits = new HashSet<string> { "base" };

            var resolver = new EntityInheritanceResolver(
                (string name, out EntityTemplate found) =>
                {
                    if (name == "base")
                    {
                        found = baseTemplate;
                        return true;
                    }

                    found = null!;
                    return false;
                });

            var effective = resolver.GetEffectiveTemplate(derived);

            Assert.Contains(child, effective.EmbeddedTemplates);
            Assert.Empty(baseTemplate.Inherits);
        }

        [Fact]
        public void Can_resolve_multilevel_inheritance()
        {
            // sanity check
            Assert.True(_repository.TryGetByName("inherited-template-level2", out var template));

            var templateWithInheritanceResolved = _inheritanceResolver.GetEffectiveTemplate(template);

            Assert.NotNull(templateWithInheritanceResolved); // sanity check

            Assert.Collection(
                templateWithInheritanceResolved.Tags.OrderBy(x => x),
                tag => Assert.Equal("tag1", tag),
                tag => Assert.Equal("tag2", tag),
                tag => Assert.Equal("tag3", tag),
                tag => Assert.Equal("tag4", tag));

            Assert.Collection(
                templateWithInheritanceResolved.Components.OrderBy(x => x.Key),
                kvp =>
                {
                    // yaml deserializer loads simple objects as key-value pairs
                    Assert.Equal("bar", kvp.Key);
                    Assert.Equal("this is also a test!", kvp.Value);
                },
                kvp =>
                {
                    Assert.Equal("barfoo", kvp.Key);
                    var valueAsDict = (Dictionary<object, object>)kvp.Value;
                    Assert.Equal("defgh", valueAsDict["anotherStringProperty"]);
                    Assert.Equal((byte)234, valueAsDict["anotherNumProperty"]);
                },
                kvp =>
                {
                    // yaml deserializer loads simple objects as key-value pairs
                    Assert.Equal("foo", kvp.Key);
                    Assert.Equal("this is a test!", kvp.Value);
                },
                kvp =>
                {
                    Assert.Equal("foobar", kvp.Key);

                    // embedded objects yaml deserializer loads as Dictionary<object, object>
                    var valueAsDict = (Dictionary<object, object>)kvp.Value;
                    Assert.Equal("abcdef", valueAsDict["stringProperty"]);
                    Assert.Equal((byte)123, valueAsDict["numProperty"]);
                });
        }

        [Fact]
        public void Effective_template_preserves_name_and_description()
        {
            Assert.True(_repository.TryGetByName("inherited-template-level1", out var template));

            var effective = _inheritanceResolver.GetEffectiveTemplate(template);

            Assert.Equal(template.Name, effective.Name);
            Assert.Equal(template.Description, effective.Description);
        }

        [Fact]
        public void Should_throw_on_self_inheritance_cycle()
        {
            var looping = new EntityTemplate { Name = "loop" };
            looping.Inherits = new HashSet<string> { "loop" };

            var resolver = new EntityInheritanceResolver(
                (string name, out EntityTemplate found) =>
                {
                    found = looping;
                    return true;
                });

            Assert.Throws<InvalidOperationException>(() => resolver.GetEffectiveTemplate(looping));
        }

        [Fact]
        public void Should_throw_on_mutual_inheritance_cycle()
        {
            var templateA = new EntityTemplate { Name = "cycle-a" };
            templateA.Inherits = new HashSet<string> { "cycle-b" };
            var templateB = new EntityTemplate { Name = "cycle-b" };
            templateB.Inherits = new HashSet<string> { "cycle-a" };

            var templates = new Dictionary<string, EntityTemplate>(StringComparer.InvariantCultureIgnoreCase)
            {
                ["cycle-a"] = templateA,
                ["cycle-b"] = templateB,
            };
            var resolver = new EntityInheritanceResolver(
                (string name, out EntityTemplate found) =>
                {
                    if (templates.TryGetValue(name, out var match))
                    {
                        found = match;
                        return true;
                    }

                    found = null!;
                    return false;
                });

            Assert.Throws<InvalidOperationException>(() => resolver.GetEffectiveTemplate(templateA));
        }

        [Fact]
        public void Diamond_inheritance_resolves_without_false_cycle()
        {
            var templateD = new EntityTemplate { Name = "diamond-d" };
            var templateB = new EntityTemplate { Name = "diamond-b" };
            templateB.Inherits = new HashSet<string> { "diamond-d" };
            var templateC = new EntityTemplate { Name = "diamond-c" };
            templateC.Inherits = new HashSet<string> { "diamond-d" };
            var templateA = new EntityTemplate { Name = "diamond-a" };
            templateA.Inherits = new HashSet<string> { "diamond-b", "diamond-c" };

            var templates = new Dictionary<string, EntityTemplate>(StringComparer.InvariantCultureIgnoreCase)
            {
                ["diamond-a"] = templateA,
                ["diamond-b"] = templateB,
                ["diamond-c"] = templateC,
                ["diamond-d"] = templateD,
            };
            var resolver = new EntityInheritanceResolver(
                (string name, out EntityTemplate found) =>
                {
                    if (templates.TryGetValue(name, out var match))
                    {
                        found = match;
                        return true;
                    }

                    found = null!;
                    return false;
                });

            var effective = resolver.GetEffectiveTemplate(templateA);
            Assert.NotNull(effective);
        }
    }
}
