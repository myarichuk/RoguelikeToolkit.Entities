using DefaultEcs;
using RoguelikeToolkit.Entities.Extensions;
using RoguelikeToolkit.Entities.Factory;

// ReSharper disable ExceptionNotDocumented
#pragma warning disable CS1591
namespace RoguelikeToolkit.Entities.Tests;

public class TemplateValueConverterTests
{
    public enum Color
    {
        Red,
        Green,
    }

    [Theory]
    [InlineData(2.0, 2)]
    [InlineData(-3.0, -3)]
    public void Whole_fractional_numbers_convert_to_integers(double value, int expected) =>
        Assert.Equal(expected, TemplateValueConverter.Convert(value, typeof(int)));

    [Theory]
    [InlineData(2.5)]
    [InlineData(2.7)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    public void Fractional_numbers_are_never_truncated_into_integers(double value) =>
        Assert.Throws<FormatException>(() => TemplateValueConverter.Convert(value, typeof(int)));

    [Fact]
    public void Fractional_decimal_is_rejected_for_integers() =>
        Assert.Throws<FormatException>(() => TemplateValueConverter.Convert(2.5m, typeof(long)));

    [Fact]
    public void Out_of_range_numbers_throw_instead_of_saturating()
    {
        // the previous general-purpose converter returned int.MaxValue here
        Assert.Throws<OverflowException>(() => TemplateValueConverter.Convert(1e20, typeof(int)));
        Assert.Throws<OverflowException>(() => TemplateValueConverter.Convert(300, typeof(byte)));
        Assert.Throws<OverflowException>(() => TemplateValueConverter.Convert(-1, typeof(uint)));
    }

    [Fact]
    public void Numbers_widen_and_narrow_when_in_range()
    {
        Assert.Equal(5L, TemplateValueConverter.Convert(5, typeof(long)));
        Assert.Equal((byte)200, TemplateValueConverter.Convert(200L, typeof(byte)));
        Assert.Equal(2.5f, TemplateValueConverter.Convert(2.5, typeof(float)));
        Assert.Equal(1.5m, TemplateValueConverter.Convert(1.5, typeof(decimal)));
        Assert.Equal(3.0, TemplateValueConverter.Convert(3, typeof(double)));
    }

    [Fact]
    public void Strings_parse_with_invariant_culture()
    {
        Assert.Equal(1.5, TemplateValueConverter.Convert("1.5", typeof(double)));
        Assert.Equal(42, TemplateValueConverter.Convert("42", typeof(int)));
        Assert.Equal(true, TemplateValueConverter.Convert("true", typeof(bool)));
        Assert.Equal('x', TemplateValueConverter.Convert("x", typeof(char)));
        Assert.Equal("1.5", TemplateValueConverter.Convert(1.5, typeof(string)));
        Assert.Throws<FormatException>(() => TemplateValueConverter.Convert("2.5", typeof(int)));
    }

    [Fact]
    public void Nullable_targets_unwrap()
    {
        Assert.Equal(7, TemplateValueConverter.Convert(7L, typeof(int?)));
        Assert.Equal(Color.Green, TemplateValueConverter.Convert("green", typeof(Color?)));
    }

    [Fact]
    public void Enums_convert_from_names_and_numbers()
    {
        Assert.Equal(Color.Green, TemplateValueConverter.Convert("GREEN", typeof(Color)));
        Assert.Equal(Color.Green, TemplateValueConverter.Convert(1, typeof(Color)));
        Assert.Throws<ArgumentException>(() => TemplateValueConverter.Convert("Blue", typeof(Color)));
        Assert.Throws<InvalidCastException>(() => TemplateValueConverter.Convert(1.0, typeof(Color)));
    }

    [Fact]
    public void Well_known_types_parse_from_strings()
    {
        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), TemplateValueConverter.Convert("00000000-0000-0000-0000-000000000001", typeof(Guid)));
        Assert.Equal(new TimeSpan(1, 2, 3), TemplateValueConverter.Convert("01:02:03", typeof(TimeSpan)));
        Assert.Equal(new DateTime(2020, 1, 2), TemplateValueConverter.Convert("2020-01-02", typeof(DateTime)));
        Assert.Equal(new DateTimeOffset(2020, 1, 2, 0, 0, 0, TimeSpan.FromHours(2)), TemplateValueConverter.Convert("2020-01-02T00:00:00+02:00", typeof(DateTimeOffset)));
        Assert.Equal(new Uri("http://example.com/"), TemplateValueConverter.Convert("http://example.com/", typeof(Uri)));
        Assert.Equal(new Version(1, 2, 3), TemplateValueConverter.Convert("1.2.3", typeof(Version)));
    }

    [Fact]
    public void Assignable_values_pass_through_untouched()
    {
        var list = new List<object>();
        Assert.Same(list, TemplateValueConverter.Convert(list, typeof(IEnumerable<object>)));
        Assert.Equal(5, TemplateValueConverter.Convert(5, typeof(object)));
    }

    [Fact]
    public void Dice_parser_is_registered_out_of_the_box_and_shares_instances()
    {
        Assert.True(TemplateValueParsers.IsRegistered(typeof(Dice)));
        var first = TemplateValueConverter.Convert("2d6+1", typeof(Dice));
        Assert.Equal("2d6+1", ((Dice)first).Expression);
        Assert.Same(first, TemplateValueConverter.Convert("2d6+1", typeof(Dice)));
        Assert.Throws<FormatException>(() => TemplateValueConverter.Convert("2d", typeof(Dice)));
    }

    [Fact]
    public void Unsupported_conversion_names_the_type_and_the_fix()
    {
        var e = Assert.Throws<InvalidCastException>(() => TemplateValueConverter.Convert("x", typeof(Unparsed)));
        Assert.Contains(typeof(Unparsed).FullName!, e.Message);
        Assert.Contains(nameof(TemplateValueParsers.Register), e.Message);
    }

    [Fact]
    public void Registered_parser_handles_custom_types_including_value_types_and_nullables()
    {
        TemplateValueParsers.Register(static s => new Point(int.Parse(s.Split(',')[0]), int.Parse(s.Split(',')[1])));
        try
        {
            Assert.Equal(new Point(3, 4), TemplateValueConverter.Convert("3,4", typeof(Point)));
            Assert.Equal(new Point(5, 6), TemplateValueConverter.Convert("5,6", typeof(Point?)));
        }
        finally
        {
            Assert.True(TemplateValueParsers.Unregister<Point>());
        }

        Assert.Throws<InvalidCastException>(() => TemplateValueConverter.Convert("3,4", typeof(Point)));
    }

    [Fact]
    public void Parser_returning_null_is_reported()
    {
        TemplateValueParsers.Register<NullParsed>(static _ => null!);
        try
        {
            Assert.Throws<InvalidOperationException>(() => TemplateValueConverter.Convert("x", typeof(NullParsed)));
        }
        finally
        {
            TemplateValueParsers.Unregister<NullParsed>();
        }
    }

    [Fact]
    public void Register_rejects_null_parser() =>
        Assert.Throws<ArgumentNullException>(() => TemplateValueParsers.Register<Point>(null!));

    [Fact]
    public void Pool_hands_back_returned_instances_and_bounds_retention()
    {
        var pool = new Pool<List<int>>(capacity: 1);
        var a = pool.Rent();
        var b = pool.Rent();
        var c = pool.Rent();
        pool.Return(a); // fast slot
        pool.Return(b); // the one slot
        pool.Return(c); // dropped

        // reference checks: xUnit's Contains would compare the (empty) lists structurally
        var rented = new[] { pool.Rent(), pool.Rent(), pool.Rent() };
        Assert.Contains(rented, r => ReferenceEquals(r, a));
        Assert.Contains(rented, r => ReferenceEquals(r, b));
        Assert.DoesNotContain(rented, r => ReferenceEquals(r, c));
    }

    [Fact]
    public void Pool_never_hands_one_instance_to_two_renters()
    {
        var pool = new Pool<HashSet<int>>();
        var owners = new System.Collections.Concurrent.ConcurrentDictionary<HashSet<int>, int>(ReferenceEqualityComparer<HashSet<int>>.Instance);
        var violations = 0;

        Parallel.For(0, 200_000, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount) }, i =>
        {
            var set = pool.Rent();
            if (!owners.TryAdd(set, i))
            {
                Interlocked.Increment(ref violations);
            }

            owners.TryRemove(set, out _);
            pool.Return(set);
        });

        Assert.Equal(0, violations);
    }

    [Fact]
    public void Type_based_component_access_round_trips()
    {
        using var world = new World();
        var entity = world.CreateEntity();

        Assert.False(entity.HasComponent(typeof(Point)));
        entity.SetComponent(typeof(Point), new Point(1, 2));
        Assert.True(entity.HasComponent(typeof(Point)));
        Assert.Equal(new Point(1, 2), entity.GetComponent(typeof(Point)));
        Assert.Equal(new Point(1, 2), entity.Get<Point>());

        Assert.Throws<ArgumentException>(() => entity.SetComponent(typeof(Point), "not a point"));
        Assert.Throws<ArgumentException>(() => entity.SetComponent(typeof(Point), null!));
        Assert.Throws<ArgumentNullException>(() => entity.HasComponent(null!));

        entity.RemoveComponent(typeof(Point));
        Assert.False(entity.HasComponent(typeof(Point)));
    }

    public readonly record struct Point(int X, int Y);

    public sealed class Unparsed
    {
    }

    public sealed class NullParsed
    {
    }

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new();

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
