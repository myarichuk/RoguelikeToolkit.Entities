using System.Runtime.CompilerServices;
using DefaultEcs;
using Microsoft.Extensions.ObjectPool;
using RoguelikeToolkit.Entities.Components;

namespace RoguelikeToolkit.Entities.Extensions
{
	/// <summary>
	/// A class with useful extensions for <see cref="Entity"/>.
	/// </summary>
	// credit: taken from https://github.com/Doraku/DefaultEcs/blob/master/source/DefaultEcs.Extension/Children/EntityExtension.cs
	// note: license is MIT, so copying this should be ok (https://github.com/Doraku/DefaultEcs/blob/master/LICENSE.md)
	// ReSharper disable once UnusedMember.Global
	public static class EntityExtension
	{
		private static readonly HashSet<string> EmptyMetadata = new();
		private static readonly HashSet<World> Worlds = new();

		/// <summary>
		/// Fetch the tag collection attached to the <paramref name="entity"/>.
		/// </summary>
		/// <param name="entity">The entity of which to fetch tags.</param>
		/// <returns>A set of tags attached to the <paramref name="entity"/>.</returns>
		public static ISet<string> Tags(this Entity entity) =>
			entity.TryGet<TagsComponent>(out var metadata) ? metadata.Value : EmptyMetadata;

		/// <summary>
		/// Check whether the <paramref name="entity"/> contains all of the <paramref name="tags"/> attached.
		/// </summary>
		/// <param name="entity">The entity of which to fetch tags.</param>
		/// <param name="tags">Tags to check their existence</param>
		/// <returns>Return true if the <paramref name="entity"/> has ALL of the <paramref name="tags"/> attached, false otherwise.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="tags"/> is <see langword="null"/>.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasTags(this Entity entity, IEnumerable<string> tags)
		{
			if (tags == null)
			{
				throw new ArgumentNullException(nameof(tags));
			}

			return entity.Tags().IsSupersetOf(tags);
		}

		/// <summary>
		/// Check whether the <paramref name="entity"/> contains the <paramref name="tag"/> attached.
		/// </summary>
		/// <param name="entity">The entity of which to fetch tags.</param>
		/// <param name="tag">Tag to check for existence</param>
		/// <returns>Return true if the <paramref name="entity"/> has the <paramref name="tag"/> attached, false otherwise.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="tag"/> is <see langword="null"/></exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasTags(this Entity entity, string tag)
		{
			if (string.IsNullOrWhiteSpace(tag))
			{
				throw new ArgumentNullException(nameof(tag));
			}

			return entity.Tags().Contains(tag);
		}

		/// <summary>
		/// Traverse the <see cref="Entity"/> parent-child tree and fetch all entities that have the specified tags
		/// </summary>
		/// <param name="parent">The starting point of parent-child tree traversal</param>
		/// <param name="tags">The tags to look for in each child <see cref="Entity"/></param>
		/// <returns>Collection of <see cref="Entity"/> references that contain the required tags</returns>
		/// <exception cref="ArgumentNullException"><paramref name="tags"/> is <see langword="null"/>.</exception>
		public static IEnumerable<Entity> GetChildrenWithTags(this Entity parent, params string[] tags)
		{
			using var iterator = new EntityChildrenIterator(parent);
			var childrenList = new List<Entity>();

			iterator.Traverse(
				entity => childrenList.Add(entity),
				entity => entity.HasTags(tags));

			return childrenList;
		}

		/// <summary>
		/// Try and fetch a certain component from an <see cref="Entity"/>
		/// </summary>
		/// <typeparam name="T">Type of the component to try and fetch</typeparam>
		/// <param name="entity">The entity to operate on</param>
		/// <param name="component">The component instance that may or may not be fetched</param>
		/// <returns>True if the required component is attached to entity, false otherwise</returns>
		/// <exception cref="Exception"><see cref="Entity" /> was not created from a <see cref="World" /> instance in use. This is not supposed to happen and is likely an issue. </exception>
		public static bool TryGet<T>(this Entity entity, out T? component)
		{
			component = default;
			if (!entity.Has<T>())
			{
				return false;
			}

			component = entity.Get<T>();
			return true;
		}

		/// <summary>
		/// Fetch all child entities from a certain <see cref="Entity"/> (recursively)
		/// </summary>
		/// <param name="parent">The entity to operate on</param>
		/// <returns>All child entities</returns>
		public static IReadOnlyList<Entity> GetChildren(this Entity parent)
		{
			using var iterator = new EntityChildrenIterator(parent);
			var childrenList = new List<Entity>();

			iterator.Traverse(
				entity => childrenList.Add(entity),
				_ => true);

			return childrenList;
		}

		/// <summary>
		/// Set <paramref name="parent"/> as parent <see cref="Entity"/> of <paramref name="child"/>
		/// </summary>
		/// <param name="parent">Future parent</param>
		/// <param name="child">Future child</param>
		/// <exception cref="InvalidOperationException"><see cref="Entity" /> was not created from a <see cref="World" />.</exception>
		/// <exception cref="Exception"><see cref="Entity" /> was not created from a <see cref="World" /> instance in use. This is not supposed to happen and is likely an issue. </exception>
		public static void SetAsParentOf(this ref Entity parent, in Entity child)
		{
			if (Worlds.Add(parent.World))
			{
				parent.World.SubscribeEntityDisposed(OnEntityDisposed);
				parent.World.SubscribeWorldDisposed(w => Worlds.Remove(w));
			}

			HashSet<Entity> children;
			if (!parent.Has<Children>())
			{
				children = new HashSet<Entity>();
				parent.Set(new Children(children));
			}
			else
			{
				children = parent.Get<Children>().Value;
			}

			children.Add(child);
		}

		/// <summary>
		/// Remove parent status for specified <paramref name="child"/>
		/// </summary>
		/// <param name="parent">Parent from which to remove the child entity</param>
		/// <param name="child">Child to remove from parent entity</param>
		/// <exception cref="Exception"><see cref="Entity" /> was not created from a <see cref="World" /> instance in use. This is not supposed to happen and is likely an issue. </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void RemoveFromParentsOf(this Entity parent, Entity child)
		{
			if (parent.Has<Children>())
			{
				parent.Get<Children>().Value.Remove(child);
			}
		}

		/// <summary>
		/// Disposal utility function to ensure children will get disposed when a parent is disposed
		/// </summary>
		/// <param name="entity">parent entity that is being disposed</param>
		private static void OnEntityDisposed(in Entity entity)
		{
			if (!entity.TryGet<Children>(out var children))
			{
				return;
			}

			entity.Remove<Children>();

			foreach (var child in children.Value.Where(child => child.IsAlive))
			{
				child.Dispose();
			}
		}

		/// <summary>
		/// Internal component used to track parent-child relationship of <see cref="Entity"/> instances
		/// </summary>
		private readonly struct Children
		{
			public readonly HashSet<Entity> Value;

			public Children(HashSet<Entity> value)
			{
				Value = value;
			}
		}

		/// <summary>
		/// Iterator helper to provide Bfs iteration over child entities
		/// </summary>
		private readonly struct EntityChildrenIterator : IDisposable
		{
			private static readonly ObjectPool<Queue<Entity>> TraverseQueuePool =
				Entities.ObjectPoolProvider.Instance.Create(new ThreadSafeObjectPoolPolicy<Queue<Entity>>());

			private static readonly ObjectPool<HashSet<Entity>> VisitedPool =
				Entities.ObjectPoolProvider.Instance.Create(new ThreadSafeObjectPoolPolicy<HashSet<Entity>>());

			private readonly HashSet<Entity> _visited;
			private readonly Queue<Entity> _traversalQueue;
			private readonly Entity _root;

			public EntityChildrenIterator(in Entity root)
			{
				_root = root;
				_visited = VisitedPool.Get();
				_traversalQueue = TraverseQueuePool.Get();
			}

			/// <summary>
			/// Traverse through all child <see cref="Entity"/> instances
			/// </summary>
			/// <param name="visitor">lambda to apply to each child encountered</param>
			/// <param name="shouldTraverse">a lambda to customize the decision whether we want to traverse over concrete <see cref="Entity"/> or not</param>
			/// <exception cref="ArgumentNullException"><paramref name="visitor"/>  or <paramref name="shouldTraverse"/> is <see langword="null"/></exception>
			public void Traverse(Action<Entity> visitor, Func<Entity, bool> shouldTraverse)
			{
				if (visitor == null)
				{
					throw new ArgumentNullException(nameof(visitor));
				}

				if (shouldTraverse == null)
				{
					throw new ArgumentNullException(nameof(shouldTraverse));
				}

				_visited.Add(_root);
				_traversalQueue.Enqueue(_root);

				while (_traversalQueue.TryDequeue(out var currentEntity))
				{
					VisitIfRelevant(visitor, shouldTraverse, currentEntity);
					EnqueueChildrenToVisitQueue(currentEntity);
				}
			}

			public void Dispose()
			{
				VisitedPool.Return(_visited);
				TraverseQueuePool.Return(_traversalQueue);
			}

			private void EnqueueChildrenToVisitQueue(Entity currentEntity)
			{
				if (!currentEntity.TryGet<Children>(out var children))
				{
					return;
				}

				foreach (var child in children.Value)
				{
					_traversalQueue.Enqueue(child);
				}
			}

			private void VisitIfRelevant(Action<Entity> visitor, Func<Entity, bool> shouldTraverse, Entity currentEntity)
			{
				if (!shouldTraverse(currentEntity) || _visited.Contains(currentEntity))
				{
					return;
				}

				if (_root != currentEntity)
				{
					visitor(currentEntity);
				}

				_visited.Add(currentEntity);
			}
		}
	}
}
