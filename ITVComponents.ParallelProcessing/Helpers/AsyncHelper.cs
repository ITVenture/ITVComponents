﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Helpers;

namespace ITVComponents.ParallelProcessing.Helpers
{
    public static class AsyncHelper
    {
        /// <summary>
        /// Begins an async await process
        /// </summary>
        /// <param name="state">the state to use for creating the async await</param>
        /// <returns>a Task that will deliver a result when the started async process is finished</returns>
        public static Task BeginAsync(object state)
        {
            return Task.Factory.FromAsync(BeginAsync, EndAsync, state);
        }

        /// <summary>
        /// Initializes the async await process
        /// </summary>
        /// <param name="callback">the callback that must be called when the task is fulfilled</param>
        /// <param name="state">the object state to set on the waithandle</param>
        /// <returns>an AsyncResult object</returns>
        public static IAsyncResult BeginAsync(AsyncCallback callback, object state)
        {
            return BeginAsync(callback, (ITask)state);
        }

        /// <summary>
        /// Begins the Async Await process with a Task
        /// </summary>
        /// <param name="callback">the callback that is called when the async process has ended</param>
        /// <param name="task">the task that is waited for</param>
        /// <returns>an AsyncResult object that is required for constructing a task</returns>
        public static IAsyncResult BeginAsync(AsyncCallback callback, ITask task)
        {
            lock (task)
            {
                if (task.AsyncHelper != null)
                {
                    return task.AsyncHelper;
                }

                return new AsyncStateModel(task, callback);
            }
        }

        /// <summary>
        /// Waits until the async process has ended and returns its result
        /// </summary>
        /// <param name="asyncObj">the async result for which to wait </param>
        /// <returns>the result of the awaited process</returns>
        public static object EndAsync(IAsyncResult asyncObj)
        {
            if (asyncObj is not AsyncStateModel asm)
            {
                throw new InvalidOperationException("This only works with Async states generated by this helper class!");
            }

            asm.AsyncWaitHandle.WaitOne();
            if (asm.AsyncState is ITask t && t.Error != null)
            {
                throw new InterProcessException(t.Error);
            }

            return null;
        }

        /// <summary>
        /// Fulfills an awaited task
        /// </summary>
        /// <param name="asyncObj">the async object that is used to await the completion</param>
        public static void Fulfill(IAsyncResult asyncObj)
        {
            if (asyncObj is not AsyncStateModel asm)
            {
                throw new InvalidOperationException("This only works with Async states generated by this helper class!");
            }

            if (asm.IsCompleted)
            {
                throw new InvalidOperationException("Already completed!");
            }

            asm.IsCompleted = true;
            ((ManualResetEvent)asyncObj.AsyncWaitHandle).Set();
            asm.WhenCompleted?.Invoke(asyncObj);
        }

        /// <summary>
        /// An internal class that is used to implement the async/await for parallelprocessing
        /// </summary>
        private class AsyncStateModel : IAsyncResult
        {
            public AsyncStateModel(ITask task, AsyncCallback whenCompleted)
            {
                task.AsyncHelper = this;
                AsyncState = task;
                WhenCompleted = whenCompleted;
                AsyncWaitHandle = new ManualResetEvent(false);
            }
            public object? AsyncState { get; }
            public AsyncCallback WhenCompleted { get; }
            public WaitHandle AsyncWaitHandle { get; }
            public bool CompletedSynchronously { get; } = false;
            public bool IsCompleted { get; internal set; }
        }
    }
}
