using Fasterflect;
using RoguelikeToolkit.DiceExpression;
using RoguelikeToolkit.Entities.Factory;
using RoguelikeToolkit.Entities.Repository;
using RoguelikeToolkit.Scripts;

// ReSharper disable ExceptionNotDocumented
// ReSharper disable ExceptionNotDocumentedOptional
#pragma warning disable CS1591
namespace RoguelikeToolkit.Entities.Tests;

public class ComponentFactoryTests
{
    internal record struct FoobarRecordStruct
    {
        public int NumProperty { get; set; }

        public string StringProperty { get; set; }
    }

    private readonly ComponentFactory _componentFactory = new();
    private readonly EntityTemplateRepository _repository = new();

    public ComponentFactoryTests()
    {
        _repository.LoadTemplateFolder("TemplatesForComponentFactory");
    }

    [Fact]
    public void Can_create_simple_class_component()
    {
        // sanity check
        Assert.True(_repository.TryGetByName("simple-template", out var template));
        Assert.True(_componentFactory.TryCreateReferenceInstance<Foobar>(
            (Dictionary<object, object>)template.Components["foobar"], out var componentInstance));

        Assert.Equal(123, componentInstance.NumProperty);
        Assert.Equal("abcdef", componentInstance.StringProperty);
    }

    [Fact]
    public void Can_create_simple_struct_component()
    {
        // sanity check
        Assert.True(_repository.TryGetByName("simple-template", out var template));
        Assert.True(_componentFactory.TryCreateReferenceInstance<FoobarStruct>(
            (Dictionary<object, object>)template.Components["foobar"], out var componentInstance));

        Assert.Equal(123, componentInstance.NumProperty);
        Assert.Equal("abcdef", componentInstance.StringProperty);
    }

    [Fact]
    public void Can_create_simple_record_struct_component()
    {
        // sanity check
        Assert.True(_repository.TryGetByName("simple-template", out var template));
        Assert.True(_componentFactory.TryCreateReferenceInstance<FoobarRecordStruct>(
            (Dictionary<object, object>)template.Components["foobar"], out var componentInstance));

        Assert.Equal(123, componentInstance.NumProperty);
        Assert.Equal("abcdef", componentInstance.StringProperty);
    }

    [Fact]
    public void Can_create_simple_class_component_with_dice()
    {
        // sanity check
        Assert.True(_repository.TryGetByName("simple-template-with-dice", out var template));
        Assert.True(_componentFactory.TryCreateReferenceInstance<DiceComponent>(
            (Dictionary<object, object>)template.Components["diceComponent"], out var componentInstance));

        Assert.Equal(
            GetAstStringFrom(Dice.Parse("3d6")),
            GetAstStringFrom(componentInstance.DiceProperty!));
    }

    [Fact]
    public void Can_create_simple_class_component_with_componentScript()
    {
        // sanity check
        Assert.True(_repository.TryGetByName("simple-template-with-script", out var template));
        Assert.True(_componentFactory.TryCreateReferenceInstance<FoobarWithComponentScript>(
            (Dictionary<object, object>)template.Components["componentWithScript"], out var componentInstance));

        Assert.Equal("component.RollResult = component.diceProperty.Roll();", GetScriptSource(componentInstance.Script!));
    }

    [Fact]
    public void Can_create_simple_class_component_with_dice_as_string()
    {
        // sanity check
        Assert.True(_repository.TryGetByName("simple-template-with-dice", out var template));
        Assert.True(_componentFactory.TryCreateReferenceInstance<DiceAsStringComponent>(
            (Dictionary<object, object>)template.Components["diceComponent"], out var componentInstance));

        Assert.Equal("3d6", componentInstance.DiceProperty);
    }

    [Fact]
    public void Can_create_simple_class_component_with_missing_property()
    {
        // sanity check
        Assert.True(_repository.TryGetByName("simple-template", out var template));
        Assert.True(_componentFactory.TryCreateReferenceInstance<PartialFoobar>(
            (Dictionary<object, object>)template.Components["foobar"], out var componentInstance));

        Assert.Equal("abcdef", componentInstance.StringProperty);
    }

    [Fact]
    public void Should_throw_on_non_string_component_key()
    {
        var objectData = new Dictionary<object, object>
        {
            [123] = "abcdef",
        };

        Assert.Throws<InvalidOperationException>(() =>
            _componentFactory.TryCreateReferenceInstance<Foobar>(objectData, out _));
    }

    [Fact]
    public void Fields_should_be_ignored_when_creating_instance()
    {
        // sanity check
        Assert.True(_repository.TryGetByName("simple-template", out var template));
        Assert.True(_componentFactory.TryCreateReferenceInstance<PartialFoobar2>(
            (Dictionary<object, object>)template.Components["foobar"], out var componentInstance));

        Assert.Equal("abcdef", componentInstance.StringProperty);

        // we ignore fields when creating component instances
        // TODO: add warning if a field matches
        Assert.Equal(0, componentInstance.NumProperty);
    }

    [Fact]
    public void Can_create_simple_class_component_with_missing_property2()
    {
        // sanity check
        Assert.True(_repository.TryGetByName("simple-template", out var template));
        Assert.True(_componentFactory.TryCreateReferenceInstance<ComplexFoobar>(
            (Dictionary<object, object>)template.Components["foobar"], out var componentInstance));

        Assert.Equal("abcdef", componentInstance.StringProperty);
        Assert.Null(componentInstance.Embedded);
    }

    [Fact]
    public void Can_create_complex_class_component()
    {
        // sanity check
        Assert.True(_repository.TryGetByName("complex-template", out var template));
        Assert.True(_componentFactory.TryCreateReferenceInstance<ComplexFoobar>(
            (Dictionary<object, object>)template.Components["foobar"], out var componentInstance));

        Assert.Equal(123, componentInstance.NumProperty);
        Assert.Equal("abcdef", componentInstance.StringProperty);

        Assert.NotNull(componentInstance.Embedded);
        Assert.Equal(234, componentInstance.Embedded.AnotherNumProperty);
        Assert.Equal("defgh", componentInstance.Embedded.AnotherStringProperty);
        Assert.Equal(234.1M, componentInstance.Embedded.DecimalProperty);
        Assert.True(componentInstance.Embedded.BoolProperty);
    }

    private static string? GetScriptSource(EntityComponentScript script)
    {
        var baseScript = script.GetFieldValue("_script", Flags.InstancePrivate);
        return baseScript.GetFieldValue("_script", Flags.InstancePrivate) as string;
    }

    [Fact]
    public void Can_create_complex_struct_component()
    {
        // sanity check
        Assert.True(_repository.TryGetByName("complex-template", out var template));
        Assert.True(_componentFactory.TryCreateReferenceInstance<ComplexFoobarAsStruct>(
            (Dictionary<object, object>)template.Components["foobar"], out var componentInstance));

        Assert.Equal(123, componentInstance.NumProperty);
        Assert.Equal("abcdef", componentInstance.StringProperty);

        Assert.Equal(234, componentInstance.Embedded.AnotherNumProperty);
        Assert.Equal("defgh", componentInstance.Embedded.AnotherStringProperty);
        Assert.Equal(234.1M, componentInstance.Embedded.DecimalProperty);
        Assert.True(componentInstance.Embedded.BoolProperty);
    }

    private string GetAstStringFrom(Dice dice)
    {
        var ast = dice.GetFieldValue("_diceAst");
        return ((dynamic)ast).ToStringTree(); // assuming antlr4 ast
    }

    internal struct FoobarStruct
    {
        public int NumProperty { get; set; }

        public string StringProperty { get; set; }
    }


    internal struct ComplexFoobarAsStruct
    {
        public int NumProperty { get; set; }

        public string StringProperty { get; set; }

        public BarFooAsStruct Embedded { get; set; }
    }

    internal struct BarFooAsStruct
    {
        public string AnotherStringProperty { get; set; }

        public int AnotherNumProperty { get; set; }

        public decimal DecimalProperty { get; set; }

        public bool BoolProperty { get; set; }
    }

    internal class FoobarWithComponentScript
    {
        public EntityComponentScript? Script { get; set; }
    }

    internal class Foobar
    {
        public int NumProperty { get; set; }

        public string? StringProperty { get; set; }
    }

    internal class PartialFoobar
    {
        public string? StringProperty { get; set; }
    }

    internal class PartialFoobar2
    {
#pragma warning disable CS0649 // intentionally never assigned: the test verifies fields are ignored
        public int NumProperty;
#pragma warning restore CS0649

        public string? StringProperty { get; set; }
    }

    internal class ComplexFoobar
    {
        public int NumProperty { get; set; }

        public string? StringProperty { get; set; }

        public BarFoo? Embedded { get; set; }
    }

    internal class BarFoo
    {
        public string? AnotherStringProperty { get; set; }

        public int AnotherNumProperty { get; set; }

        public decimal DecimalProperty { get; set; }

        public bool BoolProperty { get; set; }
    }

    internal class DiceComponent
    {
        public Dice? DiceProperty { get; set; }
    }

    internal class DiceAsStringComponent
    {
        public string? DiceProperty { get; set; }
    }
}
