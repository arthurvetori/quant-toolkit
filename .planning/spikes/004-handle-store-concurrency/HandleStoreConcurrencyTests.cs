using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HandleStoreConcurrencyTests
{
    /// <summary>
    /// Thread-safe handle store for caching QuantLib objects
    /// This is the foundation for UDF handle management
    /// </summary>
    public class HandleStore
    {
        // ConcurrentDictionary is thread-safe for basic operations
        // but we need to be careful about compound operations
        private readonly ConcurrentDictionary<string, HandleEntry> _handles =
            new ConcurrentDictionary<string, HandleEntry>();

        private class HandleEntry
        {
            public object Object { get; set; }
            public DateTime CreatedAt { get; set; }
            public int RefCount { get; set; }
        }

        /// <summary>
        /// Create or register a new handle for an object
        /// </summary>
        public string CreateHandle(object quantLibObject)
        {
            if (quantLibObject == null)
                throw new ArgumentNullException(nameof(quantLibObject));

            var handleId = Guid.NewGuid().ToString("N");
            var entry = new HandleEntry
            {
                Object = quantLibObject,
                CreatedAt = DateTime.UtcNow,
                RefCount = 1
            };

            if (!_handles.TryAdd(handleId, entry))
            {
                // Collision (extremely unlikely with GUID)
                throw new InvalidOperationException("Handle ID collision detected");
            }

            return handleId;
        }

        /// <summary>
        /// Retrieve object by handle ID
        /// Thread-safe for concurrent readers
        /// </summary>
        public object GetHandle(string handleId)
        {
            if (string.IsNullOrEmpty(handleId))
                throw new ArgumentException("Handle ID is required", nameof(handleId));

            if (_handles.TryGetValue(handleId, out var entry))
            {
                Interlocked.Increment(ref entry.RefCount);
                return entry.Object;
            }

            throw new KeyNotFoundException($"Handle not found: {handleId}");
        }

        /// <summary>
        /// Release a reference to a handle
        /// When RefCount reaches 0, object is removed
        /// </summary>
        public void ReleaseHandle(string handleId)
        {
            if (string.IsNullOrEmpty(handleId))
                return;

            if (_handles.TryGetValue(handleId, out var entry))
            {
                var newRefCount = Interlocked.Decrement(ref entry.RefCount);
                if (newRefCount <= 0)
                {
                    _handles.TryRemove(handleId, out _);
                }
            }
        }

        /// <summary>
        /// Current number of active handles
        /// </summary>
        public int Count => _handles.Count;

        /// <summary>
        /// Clear all handles (for testing)
        /// </summary>
        public void Clear() => _handles.Clear();
    }

    /// <summary>
    /// Concurrency test suite for HandleStore
    /// Validates thread safety and data integrity
    /// </summary>
    public class HandleStoreConcurrencyTests
    {
        private HandleStore _store;

        public void Setup()
        {
            _store = new HandleStore();
        }

        public void Cleanup()
        {
            _store.Clear();
        }

        /// <summary>
        /// Test 1: Concurrent handle creation
        /// Multiple threads creating handles simultaneously
        /// </summary>
        public bool Test_ConcurrentCreation()
        {
            const int threadCount = 8;
            const int handlesPerThread = 100;
            var createdHandles = new ConcurrentBag<string>();
            var exceptions = new ConcurrentBag<Exception>();

            var tasks = Enumerable.Range(0, threadCount)
                .Select(t => Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < handlesPerThread; i++)
                        {
                            var obj = new object();
                            var handleId = _store.CreateHandle(obj);
                            createdHandles.Add(handleId);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }))
                .ToArray();

            Task.WaitAll(tasks);

            // Verify
            bool success = exceptions.Count == 0 &&
                          createdHandles.Count == threadCount * handlesPerThread &&
                          _store.Count == threadCount * handlesPerThread;

            Console.WriteLine($"Test_ConcurrentCreation: {(success ? "PASS" : "FAIL")}");
            Console.WriteLine($"  Created: {createdHandles.Count} handles");
            Console.WriteLine($"  Store contains: {_store.Count} handles");
            Console.WriteLine($"  Exceptions: {exceptions.Count}");

            return success;
        }

        /// <summary>
        /// Test 2: Concurrent retrieval
        /// Multiple threads reading from same handles simultaneously
        /// </summary>
        public bool Test_ConcurrentRetrieval()
        {
            // Pre-create some handles
            var handleIds = Enumerable.Range(0, 10)
                .Select(_ => _store.CreateHandle(new object()))
                .ToList();

            const int threadCount = 8;
            const int retrievalsPerThread = 100;
            var retrievedCount = 0;
            var exceptions = new ConcurrentBag<Exception>();

            var tasks = Enumerable.Range(0, threadCount)
                .Select(t => Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < retrievalsPerThread; i++)
                        {
                            var handleId = handleIds[i % handleIds.Count];
                            var obj = _store.GetHandle(handleId);
                            if (obj != null)
                                Interlocked.Increment(ref retrievedCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }))
                .ToArray();

            Task.WaitAll(tasks);

            bool success = exceptions.Count == 0 &&
                          retrievedCount == threadCount * retrievalsPerThread;

            Console.WriteLine($"Test_ConcurrentRetrieval: {(success ? "PASS" : "FAIL")}");
            Console.WriteLine($"  Retrieved: {retrievedCount} times");
            Console.WriteLine($"  Expected: {threadCount * retrievalsPerThread}");
            Console.WriteLine($"  Exceptions: {exceptions.Count}");

            return success;
        }

        /// <summary>
        /// Test 3: Interleaved create/read/release
        /// Simulates realistic workload: creating and using handles
        /// </summary>
        public bool Test_InterleavedOperations()
        {
            const int threadCount = 8;
            const int operationsPerThread = 50;
            var handleIds = new ConcurrentBag<string>();
            var exceptions = new ConcurrentBag<Exception>();

            var tasks = Enumerable.Range(0, threadCount)
                .Select(t => Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < operationsPerThread; i++)
                        {
                            // Create a handle
                            var obj = new { Value = i, ThreadId = t };
                            var handleId = _store.CreateHandle(obj);
                            handleIds.Add(handleId);

                            // Use it
                            var retrieved = _store.GetHandle(handleId);
                            if (retrieved == null)
                                throw new InvalidOperationException("Retrieved null object");

                            // Release it
                            _store.ReleaseHandle(handleId);

                            // Small random delay to encourage interleaving
                            Thread.Sleep(Random.Shared.Next(0, 5));
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }))
                .ToArray();

            Task.WaitAll(tasks);

            bool success = exceptions.Count == 0;

            Console.WriteLine($"Test_InterleavedOperations: {(success ? "PASS" : "FAIL")}");
            Console.WriteLine($"  Operations completed: {handleIds.Count}");
            Console.WriteLine($"  Final store size: {_store.Count}");
            Console.WriteLine($"  Exceptions: {exceptions.Count}");

            return success;
        }

        /// <summary>
        /// Test 4: Rapid creation and deletion
        /// Stress test for GC and handle cleanup
        /// </summary>
        public bool Test_RapidCycling()
        {
            const int cycles = 1000;
            var exceptions = new List<Exception>();

            try
            {
                for (int i = 0; i < cycles; i++)
                {
                    var handleId = _store.CreateHandle(new object());
                    var obj = _store.GetHandle(handleId);
                    _store.ReleaseHandle(handleId);

                    // After release, handle should be gone or have RefCount 0
                    // Trying to retrieve should fail
                    try
                    {
                        _store.GetHandle(handleId);
                        // If we got here, RefCount wasn't decremented properly
                        throw new InvalidOperationException("Handle not cleaned up after release");
                    }
                    catch (KeyNotFoundException)
                    {
                        // Expected
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            bool success = exceptions.Count == 0;

            Console.WriteLine($"Test_RapidCycling: {(success ? "PASS" : "FAIL")}");
            Console.WriteLine($"  Cycles: {cycles}");
            Console.WriteLine($"  Final store size: {_store.Count}");
            Console.WriteLine($"  Exceptions: {exceptions.Count}");

            return success;
        }

        /// <summary>
        /// Run all tests
        /// </summary>
        public void RunAllTests()
        {
            Console.WriteLine("\n=== HandleStore Concurrency Test Suite ===\n");

            var tests = new[]
            {
                ("Concurrent Creation", Test_ConcurrentCreation),
                ("Concurrent Retrieval", Test_ConcurrentRetrieval),
                ("Interleaved Operations", Test_InterleavedOperations),
                ("Rapid Cycling", Test_RapidCycling)
            };

            var results = new List<(string Name, bool Passed)>();

            foreach (var (name, test) in tests)
            {
                Setup();
                try
                {
                    bool passed = test();
                    results.Add((name, passed));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Test failed with exception: {ex.Message}");
                    results.Add((name, false));
                }
                finally
                {
                    Cleanup();
                }

                Console.WriteLine();
            }

            Console.WriteLine("=== Summary ===");
            var passed = results.Count(r => r.Passed);
            var total = results.Count;
            Console.WriteLine($"Passed: {passed}/{total}");

            foreach (var (name, passed) in results)
            {
                Console.WriteLine($"  {name}: {(passed ? "✓" : "✗")}");
            }
        }
    }

    public static class Program
    {
        public static void Main()
        {
            var suite = new HandleStoreConcurrencyTests();
            suite.RunAllTests();
        }
    }
}
