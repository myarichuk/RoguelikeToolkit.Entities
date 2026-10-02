using DefaultEcs;
using RoguelikeToolkit.Entities.Components;
using RoguelikeToolkit.Entities.Extensions;

#pragma warning disable CS1591
namespace RoguelikeToolkit.Entities.Tests;

public class EntityExtensionsTests
{
    [Fact]
    public void Tagless_entity_tags_are_empty_and_shared_set_is_immutable()
    {
        using var world = new World();
        var entity = world.CreateEntity();

        Assert.Empty(entity.Tags());
        Assert.Throws<NotSupportedException>(() => entity.Tags().Add("pollution"));

        // The failed mutation must not leak into other tagless reads.
        using var otherWorld = new World();
        Assert.Empty(otherWorld.CreateEntity().Tags());
    }

    [Fact]
    public void HasTags_checks_single_and_multiple_tags()
    {
        using var world = new World();
        var entity = world.CreateEntity();
        entity.Set(new TagsComponent { Value = new HashSet<string> { "a", "b" } });

        Assert.True(entity.HasTags("a"));
        Assert.False(entity.HasTags("z"));
        Assert.True(entity.HasTags(new[] { "a", "b" }));
        Assert.False(entity.HasTags(new[] { "a", "z" }));
        Assert.Throws<ArgumentNullException>(() => entity.HasTags((string)null!));
        Assert.Throws<ArgumentNullException>(() => entity.HasTags((IEnumerable<string>)null!));
    }

    [Fact]
    public void Parent_child_link_and_unlink()
    {
        using var world = new World();
        var parent = world.CreateEntity();
        var child = world.CreateEntity();

        parent.SetAsParentOf(child);

        Assert.Single(parent.GetChildren());
        parent.RemoveFromParentsOf(child);
        Assert.Empty(parent.GetChildren());
    }

    [Fact]
    public void GetChildren_is_repeatable_with_pooled_iterators()
    {
        // Regression: pooled traversal state used to leak between calls, making the
        // second traversal skip live entities.
        using var world = new World();
        var parent = world.CreateEntity();
        var child = world.CreateEntity();
        parent.SetAsParentOf(child);

        Assert.Single(parent.GetChildren());
        Assert.Single(parent.GetChildren());
        Assert.Single(parent.GetChildren());
    }

    [Fact]
    public void Disposing_parent_disposes_children()
    {
        using var world = new World();
        var parent = world.CreateEntity();
        var child = world.CreateEntity();
        parent.SetAsParentOf(child);

        parent.Dispose();

        Assert.False(child.IsAlive);
    }

    [Fact]
    public void GetChildrenWithTags_filters_by_all_tags()
    {
        using var world = new World();
        var parent = world.CreateEntity();

        var matching = world.CreateEntity();
        matching.Set(new TagsComponent { Value = new HashSet<string> { "a", "b" } });
        parent.SetAsParentOf(matching);

        var other = world.CreateEntity();
        other.Set(new TagsComponent { Value = new HashSet<string> { "a" } });
        parent.SetAsParentOf(other);

        Assert.Single(parent.GetChildrenWithTags("a", "b"));
        Assert.Equal(2, parent.GetChildrenWithTags("a").Count());
        Assert.Empty(parent.GetChildrenWithTags("nope"));
    }
}
