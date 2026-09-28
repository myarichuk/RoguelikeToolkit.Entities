using DefaultEcs;
using RoguelikeToolkit.Entities.Components;
using RoguelikeToolkit.Entities.Extensions;
using RoguelikeToolkit.Entities.Factory;
using RoguelikeToolkit.Entities.Repository;
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
#pragma warning disable CS8766
#pragma warning disable CS1591

// ReSharper disable ExceptionNotDocumented
namespace RoguelikeToolkit.Entities.Tests
{
    public struct AnotherFoobar : IValueComponent<int>
    {
        public int Value { get; set; }
    }

    [Component(Name = "Health")]
    public struct UnitHealth : IValueComponent<decimal>
    {
        public decimal Value { get; set; }
    }

    [Component]
    public class Foobar
    {
        public int NumProperty { get; set; }

        public string? StringProperty { get; set; }
    }

    [Component]
    public class Barfoo
    {
        public int AnotherNumProperty { get; set; }

        public string? AnotherStringProperty { get; set; }
    }

    [Component(IsGlobal = true)]
    public class Attributes
    {
        public int Strength { get; set; }

        public int Agility { get; set; }
    }

    public class Foo : IValueComponent<string>
    {
        public string? Value { get; set; }
    }

    public class EntityFactoryTests
    {
        private readonly EntityFactory _entityFactory;
        private readonly EntityTemplateRepository _templateRepository = new();

        public EntityFactoryTests()
        {
            _templateRepository.LoadTemplateFolder("TemplatesForEntityFactory");
            _entityFactory = new EntityFactory(_templateRepository, new World());
        }

        [Fact]
        public void Should_return_false_non_existing_template_name() =>
            Assert.False(_entityFactory.TryCreate("non-existing", out _));

        [Fact]
        public void HasTemplateFor_should_properly_work()
        {
            Assert.False(_entityFactory.HasTemplateFor("non-existing"));
            Assert.True(_entityFactory.HasTemplateFor("template-simple"));
        }

        [Fact]
        public void Can_create_simple_entity()
        {
            Assert.True(_entityFactory.TryCreate("template-simple", out var entity));

            Assert.True(entity.Has<Foobar>());
            var fetchedFoobar = entity.Get<Foobar>();
            Assert.Equal(123, fetchedFoobar.NumProperty);
            Assert.Equal("abcdef", fetchedFoobar.StringProperty);
        }

        [Fact]
        public void Can_create_entity_with_global_component()
        {
            Assert.True(_entityFactory.TryCreate("template-with-global", out var entityA));
            Assert.True(_entityFactory.TryCreate("template-with-global", out var entityB));

            var attributesA = entityA.Get<Attributes>();
            var attributesB = entityB.Get<Attributes>();

            Assert.Same(attributesA, attributesB);
        }

        [Fact]
        public void Can_create_simple_entity_custom_name()
        {
            Assert.True(_entityFactory.TryCreate("template-simple3", out var entity));

            Assert.True(entity.Has<UnitHealth>());
            var health = entity.Get<UnitHealth>();
            Assert.Equal(123.3M, health.Value);
        }

        [Fact]
        public void Can_create_complex_entity()
        {
            Assert.True(_entityFactory.TryCreate("template-with-embedded", out var entity));

            // sanity check
            Assert.True(entity.Has<Foobar>());

            var entityTemplate2 = entity.GetChildren().FirstOrDefault(e => e.Has<Barfoo>() && e.Has<Foo>());
            Assert.NotEqual(default, entityTemplate2);

            var barfooComponent = entityTemplate2.Get<Barfoo>();
            Assert.Equal("defgh", barfooComponent.AnotherStringProperty);
            Assert.Equal(234, barfooComponent.AnotherNumProperty);

            var valueComponent = entityTemplate2.Get<Foo>();
            Assert.Equal("this is a test!", valueComponent.Value);
        }

        [Fact]
        public void Can_create_entity_with_inherit()
        {
            Assert.True(_entityFactory.TryCreate("template-with-inherit", out var entity));

            Assert.True(entity.Has<Foobar>());
            var fetchedFoobar = entity.Get<Foobar>();
            Assert.Equal(123, fetchedFoobar.NumProperty);
            Assert.Equal("abcdef", fetchedFoobar.StringProperty);

            var barfooComponent = entity.Get<Barfoo>();
            Assert.Equal("defgh", barfooComponent.AnotherStringProperty);
            Assert.Equal(234, barfooComponent.AnotherNumProperty);

            var valueComponent = entity.Get<Foo>();
            Assert.Equal("this is a test!", valueComponent.Value);
        }

        [Fact]
        public void Can_create_embedded_hierarchy_without_duplicates()
        {
            Assert.True(_entityFactory.TryCreate("template-with-embedded", out var entity));

            var descendants = entity.GetChildren();

            // root + EmbeddedTemplate1 + EmbeddedTemplate2 + EmbeddedTemplate3 (grandchild) = 3 descendants
            Assert.Equal(3, descendants.Count);

            // only the grandchild has Foobar without Barfoo/Foo; a duplicate would make this 2
            var foobarOnly = descendants.Count(e => e.Has<Foobar>() && !e.Has<Barfoo>() && !e.Has<Foo>());
            Assert.Equal(1, foobarOnly);
        }

        [Fact]
        public void Can_create_inherited_embedded_children()
        {
            var repository = new EntityTemplateRepository();
            using var baseStream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(
                    "ChildA:\n Components:\n  foobar:\n   stringProperty: abcdef\n   numProperty: 123\nComponents:\n foobar:\n  stringProperty: abcdef\n  numProperty: 123\n"));
            using var baseReader = new StreamReader(baseStream);
            repository.LoadTemplate("base-with-child", baseReader);

            using var derivedStream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("Inherits:\n - base-with-child\nComponents:\n foo: hello\n"));
            using var derivedReader = new StreamReader(derivedStream);
            repository.LoadTemplate("derived-with-child", derivedReader);

            using var world = new World();
            var factory = new EntityFactory(repository, world);

            Assert.True(factory.TryCreate("derived-with-child", out var entity));

            var descendants = entity.GetChildren();
            Assert.Single(descendants);
            Assert.True(descendants[0].Has<Foobar>());
        }

        [Fact]
        public void Should_throw_on_cyclic_embedded_templates()
        {
            var templateA = new EntityTemplate { Name = "embedded-cycle-a" };
            var templateB = new EntityTemplate { Name = "embedded-cycle-b" };
            templateA.MergeEmbeddedTemplates(new HashSet<EntityTemplate> { templateB });
            templateB.MergeEmbeddedTemplates(new HashSet<EntityTemplate> { templateA });

            Assert.Throws<InvalidOperationException>(() => _entityFactory.TryCreate(templateA, out _));
        }

        [Fact]
        public void Child_sharing_parent_name_is_still_created()
        {
            // no name-based root skipping: names are case-insensitive identifiers,
            // a child is only skipped when it is the same template reference (a cycle)
            var root = new EntityTemplate { Name = "SameName" };
            var child = new EntityTemplate { Name = "samename" };
            root.MergeEmbeddedTemplates(new HashSet<EntityTemplate> { child });

            Assert.True(_entityFactory.TryCreate(root, out var entity));
            Assert.Single(entity.GetChildren());
        }

        [Fact]
        public void Global_component_conflict_first_write_wins()
        {
            var repository = new EntityTemplateRepository();
            using var firstStream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("Components:\n attributes:\n  strength: 12\n  agility: 8\n"));
            using var firstReader = new StreamReader(firstStream);
            repository.LoadTemplate("global-first", firstReader);

            using var secondStream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("Components:\n attributes:\n  strength: 99\n  agility: 1\n"));
            using var secondReader = new StreamReader(secondStream);
            repository.LoadTemplate("global-second", secondReader);

            using var world = new World();
            var factory = new EntityFactory(repository, world);

            Assert.True(factory.TryCreate("global-first", out var entityA));
            Assert.True(factory.TryCreate("global-second", out var entityB));

            var attributesA = entityA.Get<Attributes>();
            var attributesB = entityB.Get<Attributes>();

            // first write wins: both entities share the first spawn's instance, the second values are ignored
            Assert.Same(attributesA, attributesB);
            Assert.Equal(12, attributesB.Strength);
            Assert.Equal(8, attributesB.Agility);
        }

        [Fact]
        public void Should_throw_on_null_component_value()
        {
            var repository = new EntityTemplateRepository();
            using var stream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("Components:\n foobar:\n"));
            using var reader = new StreamReader(stream);
            repository.LoadTemplate("null-component", reader);

            using var world = new World();
            var factory = new EntityFactory(repository, world);

            Assert.Throws<InvalidOperationException>(() => factory.TryCreate("null-component", out _));
        }

        [Fact]
        public void Can_create_entity_with_two_level_inherit()
        {
            Assert.True(_entityFactory.TryCreate("template-with-inherit-two-levels", out var entity));

            Assert.True(entity.Has<Foobar>());
            var fetchedFoobar = entity.Get<Foobar>();
            Assert.Equal(123, fetchedFoobar.NumProperty);
            Assert.Equal("abcdef", fetchedFoobar.StringProperty);

            var barfooComponent = entity.Get<Barfoo>();
            Assert.Equal("defgh", barfooComponent.AnotherStringProperty);
            Assert.Equal(234, barfooComponent.AnotherNumProperty);

            var valueComponent = entity.Get<Foo>();
            Assert.Equal("this is a test!", valueComponent.Value);

            var valueComponent2 = entity.Get<AnotherFoobar>();
            Assert.Equal(123, valueComponent2.Value);
        }
    }
}
