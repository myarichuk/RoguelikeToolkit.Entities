using Microsoft.Extensions.ObjectPool;

namespace RoguelikeToolkit.Entities.Factory
{
	/// <summary>
	/// <see cref="EmbeddedTemplateGraphIterator"/> is a helper object used to traverse the <see cref="EntityTemplate"/> graph
	/// </summary>
	/// <remarks>This object assumes it runs in a single thread</remarks>
	internal readonly struct EmbeddedTemplateGraphIterator : IDisposable
	{
		private static readonly ObjectPool<Queue<EntityTemplate>> QueuePool =
			ObjectPoolProvider.Instance.Create<Queue<EntityTemplate>>();

		private readonly EntityTemplate _root;
		private readonly GraphTraversalType _traversalType;
		private readonly Queue<EntityTemplate> _traversalQueue;

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
		}

		/// <summary>
		/// Traverse the graph
		/// </summary>
		/// <param name="visitorFunc">lambda that allows acting on a graph node</param>
		/// <exception cref="ArgumentNullException"><paramref name="visitorFunc"/> is <see langword="null"/></exception>
		/// <exception cref="ArgumentException">Unrecognized <see cref="GraphTraversalType"/> enum value, this is not supposed to happen and is likely a bug.</exception>
		public void Traverse(Action<EntityTemplate> visitorFunc)
		{
			if (visitorFunc == null)
			{
				throw new ArgumentNullException(nameof(visitorFunc));
			}

			_traversalQueue.Clear();

			switch (_traversalType)
			{
				case GraphTraversalType.Bfs:
					TraverseBfs(visitorFunc);
					break;
				case GraphTraversalType.Dfs:
					TraverseDfs(visitorFunc);
					break;
				default:
					throw new ArgumentException($"Unrecognized enum value {_traversalType}, this can happen only if unrecognized traversal type was added.");
			}
		}

		/// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
		public void Dispose() =>
			QueuePool.Return(_traversalQueue);

		private void TraverseBfs(Action<EntityTemplate> visitorFunc)
		{
			_traversalQueue.Enqueue(_root);

			while (_traversalQueue.TryDequeue(out var currentNode))
			{
				visitorFunc(currentNode);
				foreach (var childNode in currentNode.EmbeddedTemplates)
				{
					_traversalQueue.Enqueue(childNode);
				}
			}
		}

		private void TraverseDfs(Action<EntityTemplate> visitorFunc)
		{
			_traversalQueue.Enqueue(_root);

			while (_traversalQueue.TryDequeue(out var currentNode))
			{
				foreach (var childNode in currentNode.EmbeddedTemplates)
				{
					_traversalQueue.Enqueue(childNode);
				}

				visitorFunc(currentNode);
			}
		}
	}
}
