using System.Collections.Generic;
using RoguelikeToolkit.Entities.Components;
using Xunit;

#pragma warning disable CS1591

namespace RoguelikeToolkit.Entities.Tests;

public class TagsComponentTests
{
    [Fact]
    public void TagsComponent_Should_Set_Value()
    {
        var tags = new HashSet<string> { "tag1", "tag2" };
        var component = new TagsComponent();
        component.Value = tags;

        Assert.Equal(tags, component.Value);
    }
}