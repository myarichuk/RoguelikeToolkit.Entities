using RoguelikeToolkit.Entities.Extensions;

#pragma warning disable CS1591
namespace RoguelikeToolkit.Entities.Tests;

public class DictionaryExtensionsTests
{
    [Fact]
    public void GetOrAdd_returns_existing_without_calling_factory()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1 };
        var calls = 0;

        var result = dict.GetOrAdd("a", _ =>
        {
            calls++;
            return 2;
        });

        Assert.Equal(1, result);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void GetOrAdd_adds_missing_value_from_factory()
    {
        var dict = new Dictionary<string, int>();

        var result = dict.GetOrAdd("a", _ => 42);

        Assert.Equal(42, result);
        Assert.Equal(42, dict["a"]);
    }

    [Fact]
    public void GetOrAdd_throws_on_null_key() =>
        Assert.Throws<ArgumentNullException>(() =>
            new Dictionary<string, int>().GetOrAdd<string, int>(null!, _ => 1));

    [Fact]
    public void AddOrSet_adds_missing_key()
    {
        var dict = new Dictionary<string, int>();

        dict.AddOrSet("a", _ => 7);

        Assert.Equal(7, dict["a"]);
    }

    [Fact]
    public void AddOrSet_mutates_existing_value()
    {
        var dict = new Dictionary<string, int> { ["a"] = 7 };

        dict.AddOrSet("a", existing => existing + 1);

        Assert.Equal(8, dict["a"]);
    }

    [Fact]
    public void AddOrSet_throws_on_null_key() =>
        Assert.Throws<ArgumentNullException>(() =>
            new Dictionary<string, int>().AddOrSet(null!, _ => 1));
}
