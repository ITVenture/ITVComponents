using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.Threading
{
    public static class AsyncMonitor
    {
        private static ConcurrentDictionary<object, SemaphoreSlim> locks = new ConcurrentDictionary<object, SemaphoreSlim>();

        private static ConcurrentDictionary<SemaphoreSlim, string> lockOwners = new ConcurrentDictionary<SemaphoreSlim, string>();

        private static ConcurrentDictionary<ContextObj, int> semaphoreLocks = new ConcurrentDictionary<ContextObj, int>();

        private static AsyncLocal<string> asyncContextId = new AsyncLocal<string>();

        /// <summary>
        /// Locks a resource in a way that is compatible to async processing
        /// </summary>
        /// <param name="obj">the object that is being locked</param>
        public static void Enter(object obj)
        {
            string currentContext = EnsureContextId();
            var semaphore = locks.GetOrAdd(obj, o => new SemaphoreSlim(0, 1));
            if (!semaphore.Wait(100))
            {
                if (lockOwners.GetOrAdd(semaphore, s => currentContext) == currentContext)
                {
                    semaphoreLocks.AddOrUpdate(new ContextObj
                    {
                        Context = currentContext,
                        Resource = obj
                    }, a => 1, (a, u) => u + 1);
                }
                else
                {
                    semaphore.Wait();
                    lockOwners[semaphore] = currentContext;
                    semaphoreLocks.AddOrUpdate(new ContextObj
                    {
                        Context = currentContext,
                        Resource = obj
                    }, a => 1, (a, u) => u + 1);
                }
            }
            else
            {
                lockOwners[semaphore] = currentContext;
                semaphoreLocks.AddOrUpdate(new ContextObj
                {
                    Context = currentContext,
                    Resource = obj
                }, a => 1, (a, u) => u + 1);
            }
        }

        /// <summary>
        /// Attempts to lock a resource in a way that is compatible to async processing
        /// </summary>
        /// <param name="obj">the object that is being locked</param>
        /// <param name="timeout">the timeout to wait for the lock acquisition</param>
        /// <returns>a value indicating whether the resource could be locked</returns>
        public static bool TryEnter(object obj, int timeout)
        {
            string currentContext = EnsureContextId();
            var semaphore = locks.GetOrAdd(obj, o => new SemaphoreSlim(0, 1));
            if (!semaphore.Wait(100))
            {
                if (lockOwners.GetOrAdd(semaphore, s => currentContext) == currentContext)
                {
                    semaphoreLocks.AddOrUpdate(new ContextObj
                    {
                        Context = currentContext,
                        Resource = obj
                    }, a => 1, (a, u) => u + 1);
                }
                else
                {
                    if (semaphore.Wait(timeout))
                    {
                        lockOwners[semaphore] = currentContext;
                        semaphoreLocks.AddOrUpdate(new ContextObj
                        {
                            Context = currentContext,
                            Resource = obj
                        }, a => 1, (a, u) => u + 1);
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                lockOwners[semaphore] = currentContext;
                semaphoreLocks.AddOrUpdate(new ContextObj
                {
                    Context = currentContext,
                    Resource = obj
                }, a => 1, (a, u) => u + 1);
            }

            return true;
        }

        /// <summary>
        /// Gets a value indicating whether the given resource object is locked in the current async context
        /// </summary>
        /// <param name="obj">the object for which to check whether it is locked</param>
        /// <returns>a value indicating whether the resource is locked by the current context</returns>
        public static bool IsEntered(object obj)
        {
            string currentContext = EnsureContextId();
            var semaphore = locks.GetOrAdd(obj, o => new SemaphoreSlim(0, 1));
            return lockOwners.ContainsKey(semaphore) && lockOwners[semaphore] == currentContext;
        }

        /// <summary>
        /// Releases the Resource-Lock for the given object in the current context
        /// </summary>
        /// <param name="obj">the object that is currently locked by the current context</param>
        public static void Exit(object obj)
        {
            string currentContext = EnsureContextId();
            if (locks.TryGetValue(obj, out var sem) && lockOwners.ContainsKey(sem) && lockOwners[sem] == currentContext)
            {
                var co = new ContextObj
                {
                    Context = currentContext,
                    Resource = obj
                };
                var open = semaphoreLocks.AddOrUpdate(co, a => 0, (a, u) => u - 1);

                if (open == 0)
                {
                    semaphoreLocks.TryRemove(co, out _);
                    lockOwners.TryRemove(sem, out _);
                    sem.Release();
                }
            }
            else
            {
                throw new InvalidOperationException("The given resource is not locked by this context");
            }
        }


        private static string EnsureContextId()
        {
            if (string.IsNullOrEmpty(asyncContextId.Value))
            {
                asyncContextId.Value = Guid.NewGuid().ToString();
            }

            return asyncContextId.Value;
        }

        private class ContextObj : IEquatable<ContextObj>
        {
            public object Resource { get; set; }
            public string Context { get; set; }

            public override bool Equals(object? obj)
            {
                var retVal = false;
                if (obj is ContextObj cox)
                {
                    retVal = Equals(cox);
                }

                return retVal;
            }

            public bool Equals(ContextObj other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Equals(Resource, other.Resource) && Context == other.Context;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Resource, Context);
            }
        }
    }
}
