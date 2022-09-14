using System.Collections.Concurrent;
using Microsoft.Extensions.ObjectPool;

namespace RoguelikeToolkit.Entities
{
	/// <summary>
	/// An object pool policy to create/fetch pooled objects that is thread-safe.
	/// </summary>
	/// <typeparam name="TObject">Object type to pool.</typeparam>
	internal class ThreadSafeObjectPoolPolicy<TObject> : IPooledObjectPolicy<TObject>
		where TObject : class, new()
	{
		private readonly ConcurrentQueue<TObject> _pool = new();

		/// <summary>
		/// Get cached or new instance of the object.
		/// </summary>
		/// <returns>TObject instance.</returns>
		public TObject Create() =>
			_pool.TryDequeue(out var instance) ? instance : new TObject();

		/// <summary>
		/// "Return" the instance of the object to the pool, this will ensure the instance can be reused later.
		/// </summary>
		/// <param name="obj">the object instance to return.</param>
		/// <returns>true if the object was successfully returned to the pool.</returns>
		public bool Return(TObject obj)
		{
			_pool.Enqueue(obj);
			return true;
		}
	}
}
