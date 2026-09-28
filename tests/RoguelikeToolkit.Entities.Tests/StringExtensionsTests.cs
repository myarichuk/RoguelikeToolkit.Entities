using RoguelikeToolkit.Entities.Extensions;
using Xunit;

#pragma warning disable CS1591

namespace RoguelikeToolkit.Entities.Tests;

public class StringExtensionsTests
{
    [Fact]
    public void Trim_Should_Remove_Correct_Amount_Of_Characters()
    {
        var input = "Hello World";

        var result = input.Trim(2, 2);

        Assert.Equal("llo Wor", result);
    }

    [Fact]
    public void Trim_Should_Remove_Correct_Amount_Of_Characters_From_Beginning()
    {
        var input = "Hello World";

        var result = input.Trim(2);

        Assert.Equal("llo World", result);
    }
}
