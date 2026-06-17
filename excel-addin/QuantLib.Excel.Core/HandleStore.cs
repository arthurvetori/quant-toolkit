using System;
using System.Collections.Concurrent;
using System.Threading;

namespace QuantLib.Excel.Core
{
    /// <summary>
    /// Thread-safe handle store for QuantLib objects.
    /// Stores objects with opaque string handles and atomic reference counting.
    /// </summary>
    public static class HandleStore
    {
        private static readonly ConcurrentDictionary<string, HandleEntry> _handles 
            = new ConcurrentDictionary<string, HandleEntry>();

        public class HandleEntry
        {
            public object? Object { get; set; }
            public long RefCount = 1;
        }

        /// <summary>
        /// Creates a new handle for an object and stores it.
        /// Returns an opaque string handle.
        /// </summary>
        public static string Create(object quantLibObject)
        {
            if (quantLibObject == null)
                throw new ArgumentNullException(nameof(quantLibObject));

            var id = Guid.NewGuid().ToString("N");
            var entry = new HandleEntry { Object = quantLibObject, RefCount = 1 };

            if (!_handles.TryAdd(id, entry))
                throw new InvalidOperationException($"Failed to create handle for {quantLibObject.GetType().Name}");

            return id;
        }

        /// <summary>
        /// Retrieves an object by handle.
        /// Throws KeyNotFoundException if handle is invalid.
        /// </summary>
        public static T Get<T>(string handle) where T : class
        {
            if (string.IsNullOrWhiteSpace(handle))
                throw new ArgumentException("Handle cannot be null or empty", nameof(handle));

            if (!_handles.TryGetValue(handle, out var entry))
                throw new KeyNotFoundException($"Handle '{handle}' not found in store");

            if (entry?.Object is not T typedObject)
                throw new InvalidCastException($"Handle '{handle}' does not contain an object of type {typeof(T).Name}");

            return typedObject;
        }

        /// <summary>
        /// Retrieves an object by handle as System.Object.
        /// </summary>
        public static object? GetObject(string handle)
        {
            if (string.IsNullOrWhiteSpace(handle))
                throw new ArgumentException("Handle cannot be null or empty", nameof(handle));

            if (!_handles.TryGetValue(handle, out var entry))
                throw new KeyNotFoundException($"Handle '{handle}' not found in store");

            return entry?.Object;
        }

        /// <summary>
        /// Atomically increments the reference count for a handle.
        /// </summary>
        public static void IncrementRef(string handle)
        {
            if (string.IsNullOrWhiteSpace(handle))
                throw new ArgumentException("Handle cannot be null or empty", nameof(handle));

            if (!_handles.TryGetValue(handle, out var entry))
                throw new KeyNotFoundException($"Handle '{handle}' not found in store");

            Interlocked.Increment(ref entry.RefCount);
        }

        /// <summary>
        /// Atomically decrements the reference count for a handle.
        /// If RefCount reaches 0, removes the handle.
        /// </summary>
        public static void DecrementRef(string handle)
        {
            if (string.IsNullOrWhiteSpace(handle))
                throw new ArgumentException("Handle cannot be null or empty", nameof(handle));

            if (!_handles.TryGetValue(handle, out var entry))
                throw new KeyNotFoundException($"Handle '{handle}' not found in store");

            long newRefCount = Interlocked.Decrement(ref entry.RefCount);
            if (newRefCount <= 0)
            {
                _handles.TryRemove(handle, out _);
            }
        }

        /// <summary>
        /// Returns the reference count for a handle.
        /// </summary>
        public static long GetRefCount(string handle)
        {
            if (string.IsNullOrWhiteSpace(handle))
                throw new ArgumentException("Handle cannot be null or empty", nameof(handle));

            if (!_handles.TryGetValue(handle, out var entry))
                throw new KeyNotFoundException($"Handle '{handle}' not found in store");

            return Interlocked.Read(ref entry.RefCount);
        }

        /// <summary>
        /// Checks if a handle exists in the store.
        /// </summary>
        public static bool Exists(string handle)
        {
            return !string.IsNullOrWhiteSpace(handle) && _handles.ContainsKey(handle);
        }

        /// <summary>
        /// Removes a handle from the store.
        /// </summary>
        public static bool Remove(string handle)
        {
            if (string.IsNullOrWhiteSpace(handle))
                return false;

            return _handles.TryRemove(handle, out _);
        }

        /// <summary>
        /// Clears all handles from the store.
        /// </summary>
        public static void Clear()
        {
            _handles.Clear();
        }

        /// <summary>
        /// Returns the number of handles currently stored.
        /// </summary>
        public static int Count => _handles.Count;
    }
}
