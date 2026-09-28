using RoguelikeToolkit.Entities.Factory;
#pragma warning disable CS1591

// ReSharper disable ExceptionNotDocumented
namespace RoguelikeToolkit.Entities.Tests
{
    public class GraphTraversalTests
    {
        private static EntityTemplate Named(string name) =>
            new() { Name = name };

        private static List<string> TraverseNames(EntityTemplate root, GraphTraversalType traversalType)
        {
            using var iterator = new EmbeddedTemplateGraphIterator(root, traversalType);
            var visited = new List<string>();
            iterator.Traverse(template => visited.Add(template.Name!));
            return visited;
        }

        [Fact]
        public void Bfs_visits_level_by_level()
        {
            // root -> {left, right}, left -> {grandchild} : BFS must finish the level before descending
            var grandchild = Named("grandchild");
            var left = Named("left");
            left.MergeEmbeddedTemplates(new HashSet<EntityTemplate> { grandchild });
            var right = Named("right");
            var root = Named("root");
            root.MergeEmbeddedTemplates(new HashSet<EntityTemplate> { left, right });

            var visited = TraverseNames(root, GraphTraversalType.Bfs);

            Assert.Equal("root", visited[0]);
            Assert.Equal(4, visited.Count);

            // grandchild comes after both level-1 nodes regardless of sibling order
            Assert.True(visited.IndexOf("left") < visited.IndexOf("grandchild"));
            Assert.True(visited.IndexOf("right") < visited.IndexOf("grandchild"));
        }

        [Fact]
        public void Dfs_is_preorder_not_level_order()
        {
            // a single-child chain has a fixed enumeration order, so the exact sequence is deterministic
            var grandchild = Named("grandchild");
            var child = Named("child");
            child.MergeEmbeddedTemplates(new HashSet<EntityTemplate> { grandchild });
            var root = Named("root");
            root.MergeEmbeddedTemplates(new HashSet<EntityTemplate> { child });

            var visited = TraverseNames(root, GraphTraversalType.Dfs);

            Assert.Equal(new[] { "root", "child", "grandchild" }, visited);
        }

        [Fact]
        public void Dfs_visits_parent_before_child_in_branching_graph()
        {
            var grandchild = Named("grandchild");
            var left = Named("left");
            left.MergeEmbeddedTemplates(new HashSet<EntityTemplate> { grandchild });
            var right = Named("right");
            var root = Named("root");
            root.MergeEmbeddedTemplates(new HashSet<EntityTemplate> { left, right });

            var visited = TraverseNames(root, GraphTraversalType.Dfs);

            Assert.Equal("root", visited[0]);
            Assert.Equal(4, visited.Count);

            // pre-order property: a parent always precedes its own subtree
            Assert.True(visited.IndexOf("root") < visited.IndexOf("left"));
            Assert.True(visited.IndexOf("root") < visited.IndexOf("right"));
            Assert.True(visited.IndexOf("left") < visited.IndexOf("grandchild"));
        }
    }
}
