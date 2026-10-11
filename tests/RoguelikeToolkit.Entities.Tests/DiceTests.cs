#pragma warning disable CS1591

namespace RoguelikeToolkit.Entities.Tests;

public class DiceTests
{
    [Theory]
    [InlineData("7", 7)]
    [InlineData("2+3*4", 14)]
    [InlineData("(2+3)*4", 20)]
    [InlineData("10-4-3", 3)]
    [InlineData("100/10/5", 2)]
    [InlineData("7/2", 3)]
    [InlineData("-7/2", -3)]
    [InlineData("-3+5", 2)]
    [InlineData("--3", 3)]
    [InlineData("2*-3", -6)]
    [InlineData(" ( 1 + 2 ) * ( 3 + 4 ) ", 21)]
    public void Constant_arithmetic_follows_precedence_and_associativity(string expression, int expected) =>
        Assert.Equal(expected, Dice.Parse(expression).Roll());

    [Theory]
    [InlineData("3d6", new[] { 1, 2, 3 }, 6)]
    [InlineData("3D6", new[] { 1, 2, 3 }, 6)]
    [InlineData("d6", new[] { 4 }, 4)]
    [InlineData("2d6+3", new[] { 6, 6 }, 15)]
    [InlineData("2 d 6 + 3", new[] { 6, 6 }, 15)]
    [InlineData("4d6k3", new[] { 1, 5, 3, 6 }, 14)]
    [InlineData("4d6kh3", new[] { 1, 5, 3, 6 }, 14)]
    [InlineData("4d6kl1", new[] { 4, 5, 2, 6 }, 2)]
    [InlineData("2d20kl1+5", new[] { 17, 3 }, 8)]
    [InlineData("3k2", new[] { 9, 1, 10 }, 19)]
    [InlineData("d%", new[] { 57 }, 57)]
    [InlineData("2d%", new[] { 57, 43 }, 100)]
    [InlineData("(1d8+2)*2", new[] { 5 }, 14)]
    [InlineData("1d6-1d6", new[] { 2, 5 }, -3)]
    [InlineData("3d6k3", new[] { 1, 2, 3 }, 6)]
    public void Dice_terms_sum_and_keep_the_drawn_values(string expression, int[] draws, int expected) =>
        Assert.Equal(expected, Dice.Parse(expression).Roll(new SequenceRandom(draws)));

    [Theory]
    [InlineData("d6", 6)]
    [InlineData("d%", 100)]
    [InlineData("3k2", 10)]
    [InlineData("d1", 1)]
    public void Dice_draw_from_one_to_sides_inclusive(string expression, int sides)
    {
        var random = new SequenceRandom(1);
        Dice.Parse(expression).Roll(random);

        Assert.Equal(1, random.LastMin);
        Assert.Equal(sides + 1, random.LastMaxExclusive);
    }

    [Fact]
    public void Real_rolls_cover_every_face_and_never_leave_range()
    {
        // Regression: the previous implementation drew Next(1, sides), so a d6 never rolled a 6.
        var dice = Dice.Parse("1d6");
        var random = new Random(1234);
        var seen = new bool[7];
        for (var i = 0; i < 10_000; i++)
        {
            var roll = dice.Roll(random);
            Assert.InRange(roll, 1, 6);
            seen[roll] = true;
        }

        Assert.All(Enumerable.Range(1, 6), face => Assert.True(seen[face], $"face {face} never rolled"));
    }

    [Fact]
    public void Seeded_rolls_are_reproducible()
    {
        var dice = Dice.Parse("10d10k5+2d20kl1*3");
        var first = new int[100];
        var second = new int[100];

        var a = new Random(99);
        var b = new Random(99);
        for (var i = 0; i < 100; i++)
        {
            first[i] = dice.Roll(a);
            second[i] = dice.Roll(b);
        }

        Assert.Equal(first, second);
    }

    [Fact]
    public void Default_roll_is_thread_safe()
    {
        var dice = Dice.Parse("4d6k3");
        Parallel.For(0, 10_000, _ => Assert.InRange(dice.Roll(), 3, 18));
    }

    [Fact]
    public void Expression_is_the_trimmed_source() =>
        Assert.Equal("2d6 + 3", Dice.Parse("  2d6 + 3 ").ToString());

    [Fact]
    public void Deeply_nested_input_beyond_stackalloc_threshold_still_evaluates()
    {
        // Right-leaning chain forces a deep evaluation stack: 1+(1+(1+...)).
        var expression = string.Concat(Enumerable.Repeat("1+(", 60)) + "1" + new string(')', 60);
        Assert.Equal(61, Dice.Parse(expression).Roll());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("3d")]
    [InlineData("d")]
    [InlineData("d0")]
    [InlineData("0d6")]
    [InlineData("1001d6")]
    [InlineData("1d1000001")]
    [InlineData("3d6k4")]
    [InlineData("3d6k0")]
    [InlineData("3d6k")]
    [InlineData("3d6kx1")]
    [InlineData("k3")]
    [InlineData("1d6)")]
    [InlineData("(1d6")]
    [InlineData("2+")]
    [InlineData("*2")]
    [InlineData("abc")]
    [InlineData("1d6 2")]
    [InlineData("99999999999")]
    public void Malformed_expressions_are_rejected(string expression)
    {
        Assert.False(Dice.TryParse(expression, out var dice));
        Assert.Null(dice);
        var ex = Assert.Throws<FormatException>(() => Dice.Parse(expression));
        Assert.Contains("Invalid dice expression", ex.Message);
    }

    [Fact]
    public void Nesting_is_bounded()
    {
        var expression = new string('(', 1000) + "1" + new string(')', 1000);
        Assert.Throws<FormatException>(() => Dice.Parse(expression));
    }

    [Fact]
    public void Null_input_is_handled()
    {
        Assert.False(Dice.TryParse(null, out _));
        Assert.Throws<ArgumentNullException>(() => Dice.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => Dice.Parse("1d6").Roll(null!));
    }

    [Fact]
    public void Division_by_zero_and_overflow_throw_at_roll_time()
    {
        Assert.Throws<DivideByZeroException>(() => Dice.Parse("1/(1d1-1)").Roll());
        Assert.Throws<OverflowException>(() => Dice.Parse("2147483647+1").Roll());
    }

    private sealed class SequenceRandom : Random
    {
        private readonly int[] _values;
        private int _index;

        public SequenceRandom(params int[] values) => _values = values;

        public int LastMin { get; private set; }

        public int LastMaxExclusive { get; private set; }

        public override int Next(int minValue, int maxValue)
        {
            LastMin = minValue;
            LastMaxExclusive = maxValue;
            return _values[_index++ % _values.Length];
        }
    }
}
