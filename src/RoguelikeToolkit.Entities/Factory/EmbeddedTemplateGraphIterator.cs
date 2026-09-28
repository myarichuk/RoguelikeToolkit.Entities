using Microsoft.Extensions.ObjectPool;

namespace RoguelikeToolkit.Entities.Factory
{
    /// <summary>
    /// <see cref="EmbeddedTemplateGraphIterator"/> is a helper object used to traverse the <see cref="EntityTemplate"/> graph
    /// </summary>
    /// <remarks>
    /// This object assumes it runs in a single thread (a <see langword="readonly"/> struct over a mutable
    /// pooled <see cref="Queue{T}"/>/<see cref="Stack{T}"/> cannot be shared across threads).
    /// Pooled containers are <c>Clear</c>ed both before use and on <see cref="Dispose"/>, so a returned
    /// container retains no template references.
    /// </remarks>
    internal readonly struct EmbeddedTemplateGraphIterator : IDisposable
    {
        private static readonly ObjectPool<Queue<EntityTemplate>> QueuePool =
            ObjectPoolProvider.Instance.Create<Queue<EntityTemplate>>();

        private static readonly ObjectPool<Stack<EntityTemplate>> StackPool =
            ObjectPoolProvider.Instance.Create<Stack<EntityTemplate>>();

        private readonly EntityTemplate _root;
        private readonly GraphTraversalType _traversalType;
        private readonly Queue<EntityTemplate> _traversalQueue;
        private readonly Stack<EntityTemplate> _traversalStack;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedTemplateGraphIterator"/> struct
        /// </summary>
        /// <param name="root">Starting point of the iteration</param>
        /// <param name="traversalType">A switch to set the iteration type</param>
        public EmbeddedTemplateGraphIterator(EntityTemplate root, GraphTraversalType traversalType = GraphTraversalType.Bfs)
        {
            _root = root;
            _traversalType = traversalType;
            _traversalQueue = QueuePool.Get();
            _traversalStack = StackPool.Get();
        }

        /// <summary>
        /// A delegate for the action to apply for each embedded entity template
        /// </summary>
        /// <param name="template">current entity template</param>
        internal delegate void VisitorAction(EntityTemplate template);

        /// <summary>
        /// Traverse the graph
        /// </summary>
        /// <param name="visitorFunc">lambda that allows acting on a graph node</param>
        /// <exception cref="ArgumentNullException"><paramref name="visitorFunc"/> is <see langword="null"/></exception>
        /// <exception cref="ArgumentException">Unrecognized <see cref="GraphTraversalType"/> enum value, this is not supposed to happen and is likely an issue that should be reported.</exception>
        public void Traverse(VisitorAction visitorFunc)
        {
            if (visitorFunc == null)
            {
                throw new ArgumentNullException(nameof(visitorFunc));
            }

            _traversalQueue.Clear();
            _traversalStack.Clear();

            switch (_traversalType)
            {
                case GraphTraversalType.Bfs:
                    _traversalQueue.Enqueue(_root);
                    TraverseBfs(visitorFunc);
                    break;
                case GraphTraversalType.Dfs:
                    _traversalStack.Push(_root);
                    TraverseDfs(visitorFunc);
                    break;
                default:
                    throw new ArgumentException($"Unrecognized enum value {_traversalType}, this can happen only if unrecognized traversal type was added.");
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            _traversalQueue.Clear();
            QueuePool.Return(_traversalQueue);
            _traversalStack.Clear();
            StackPool.Return(_traversalStack);
        }

        private void TraverseBfs(VisitorAction visitorFunc)
        {
            while (_traversalQueue.TryDequeue(out var currentNode))
            {
                visitorFunc(currentNode);
                foreach (var childNode in currentNode.EmbeddedTemplates)
                {
                    _traversalQueue.Enqueue(childNode);
                }
            }
        }

        private void TraverseDfs(VisitorAction visitorFunc)
        {
            while (_traversalStack.TryPop(out var currentNode))
            {
                visitorFunc(currentNode);

                // push in reverse so that enumeration order is preserved when popping (pre-order DFS)
                foreach (var childNode in currentNode.EmbeddedTemplates.Reverse())
                {
                    _traversalStack.Push(childNode);
                }
            }
        }
    }
}
