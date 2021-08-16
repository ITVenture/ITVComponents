using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.Helpers
{
    /// <summary>
    /// Provides extensions for Async processing
    /// </summary>
    public static class AsyncExtensions
    {
        /// <summary>
        /// Runs a task with a timeout
        /// </summary>
        /// <typeparam name="TResult">the expected result type</typeparam>
        /// <param name="startTask">the task to be executed</param>
        /// <param name="timeout">the timeout for the action</param>
        /// <returns>a task that will be cancelled automatically after the given timeout</returns>
        public static async Task<TResult> CancelAfterAsync<TResult>(Func<CancellationToken, Task<TResult>> startTask, TimeSpan timeout)
        {
            using (var timeoutCancellation = new CancellationTokenSource())
            {
                return await RunWithCt<TResult>(startTask, timeout, null, timeoutCancellation);
            }
        }

        /// <summary>
        /// Runs a task with a timeout
        /// </summary>
        /// <typeparam name="TResult">the expected result type</typeparam>
        /// <param name="startTask">the task to be executed</param>
        /// <param name="timeout">the timeout for the action</param>
        /// <param name="cancellationToken">the cancellation token that can be used to cancel the action from the caller</param>
        /// <returns>a task that will be cancelled automatically after the given timeout</returns>
        public static async Task<TResult> CancelAfterAsync<TResult>(Func<CancellationToken, Task<TResult>> startTask, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using (var timeoutCancellation = new CancellationTokenSource())
            using (var combinedCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token))
            {
                return await RunWithCt<TResult>(startTask, timeout, null, combinedCancellation);
            }
        }

        /// <summary>
        /// Runs a task with a timeout
        /// </summary>
        /// <typeparam name="TResult">the expected result type</typeparam>
        /// <param name="startTask">the task to be executed</param>
        /// <param name="timeout">the timeout for the action</param>
        /// <returns>a task that will be cancelled automatically after the given timeout</returns>
        public static async Task CancelAfterAsync(Func<CancellationToken, Task> startTask, TimeSpan timeout)
        {
            using (var timeoutCancellation = new CancellationTokenSource())
            {
                await RunWithCt(startTask, timeout, null, timeoutCancellation);
            }
        }

        /// <summary>
        /// Runs a task with a timeout
        /// </summary>
        /// <typeparam name="TResult">the expected result type</typeparam>
        /// <param name="startTask">the task to be executed</param>
        /// <param name="timeout">the timeout for the action</param>
        /// <param name="cancellationToken">the cancellation token that can be used to cancel the action from the caller</param>
        /// <returns>a task that will be cancelled automatically after the given timeout</returns>
        public static async Task CancelAfterAsync(Func<CancellationToken, Task> startTask, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using (var timeoutCancellation = new CancellationTokenSource())
            using (var combinedCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token))
            {
                await RunWithCt(startTask, timeout, null, combinedCancellation);
            }
        }

        //---
        /// <summary>
        /// Runs a task with a timeout
        /// </summary>
        /// <typeparam name="TResult">the expected result type</typeparam>
        /// <param name="startTask">the task to be executed</param>
        /// <param name="timeout">the timeout for the action</param>
        /// <param name="cancelAction">the action that will be invoked, when the task times out, before the timeout exception is thrown</param>
        /// <returns>a task that will be cancelled automatically after the given timeout</returns>
        public static async Task<TResult> CancelAfterAsync<TResult>(Func<CancellationToken, Task<TResult>> startTask, TimeSpan timeout, Action<Task<TResult>> cancelAction)
        {
            using (var timeoutCancellation = new CancellationTokenSource())
            {
                return await RunWithCt<TResult>(startTask, timeout, cancelAction, timeoutCancellation);
            }
        }

        /// <summary>
        /// Runs a task with a timeout
        /// </summary>
        /// <typeparam name="TResult">the expected result type</typeparam>
        /// <param name="startTask">the task to be executed</param>
        /// <param name="timeout">the timeout for the action</param>
        /// <param name="cancelAction">the action that will be invoked, when the task times out, before the timeout exception is thrown</param>
        /// <param name="cancellationToken">the cancellation token that can be used to cancel the action from the caller</param>
        /// <returns>a task that will be cancelled automatically after the given timeout</returns>
        public static async Task<TResult> CancelAfterAsync<TResult>(Func<CancellationToken, Task<TResult>> startTask, TimeSpan timeout, Action<Task<TResult>> cancelAction, CancellationToken cancellationToken)
        {
            using (var timeoutCancellation = new CancellationTokenSource())
            using (var combinedCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token))
            {
                return await RunWithCt<TResult>(startTask, timeout, cancelAction, combinedCancellation);
            }
        }

        /// <summary>
        /// Runs a task with a timeout
        /// </summary>
        /// <typeparam name="TResult">the expected result type</typeparam>
        /// <param name="startTask">the task to be executed</param>
        /// <param name="timeout">the timeout for the action</param>
        /// <param name="cancelAction">the action that will be invoked, when the task times out, before the timeout exception is thrown</param>
        /// <returns>a task that will be cancelled automatically after the given timeout</returns>
        public static async Task CancelAfterAsync(Func<CancellationToken, Task> startTask, TimeSpan timeout, Action<Task> cancelAction)
        {
            using (var timeoutCancellation = new CancellationTokenSource())
            {
                await RunWithCt(startTask, timeout, cancelAction, timeoutCancellation);
            }
        }

        /// <summary>
        /// Runs a task with a timeout
        /// </summary>
        /// <typeparam name="TResult">the expected result type</typeparam>
        /// <param name="startTask">the task to be executed</param>
        /// <param name="timeout">the timeout for the action</param>
        /// <param name="cancelAction">the action that will be invoked, when the task times out, before the timeout exception is thrown</param>
        /// <param name="cancellationToken">the cancellation token that can be used to cancel the action from the caller</param>
        /// <returns>a task that will be cancelled automatically after the given timeout</returns>
        public static async Task CancelAfterAsync(Func<CancellationToken, Task> startTask, TimeSpan timeout, Action<Task> cancelAction, CancellationToken cancellationToken)
        {
            using (var timeoutCancellation = new CancellationTokenSource())
            using (var combinedCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token))
            {
                await RunWithCt(startTask, timeout, cancelAction, combinedCancellation);
            }
        }

        private static async Task RunWithCt(Func<CancellationToken, Task> startTask, TimeSpan timeout, Action<Task> cancelAction, CancellationTokenSource cancellation)
        {
            var originalTask = startTask(cancellation.Token);
            var delayTask = Task.Delay(timeout, cancellation.Token);
            var completedTask = await Task.WhenAny(originalTask, delayTask);
            // Cancel timeout to stop either task:
            // - Either the original task completed, so we need to cancel the delay task.
            // - Or the timeout expired, so we need to cancel the original task.
            // Canceling will not affect a task, that is already completed.
            cancellation.Cancel();
            if (completedTask == originalTask)
            {
                // original task completed
                await originalTask;
            }
            else
            {
                cancelAction?.Invoke(originalTask);
                // timeout
                throw new TimeoutException();
            }
        }

        private static async Task<TResult> RunWithCt<TResult>(Func<CancellationToken, Task<TResult>> startTask, TimeSpan timeout, Action<Task<TResult>> cancelAction, CancellationTokenSource cancellation)
        {
            var originalTask = startTask(cancellation.Token);
            var delayTask = Task.Delay(timeout, cancellation.Token);
            var completedTask = await Task.WhenAny(originalTask, delayTask);
            // Cancel timeout to stop either task:
            // - Either the original task completed, so we need to cancel the delay task.
            // - Or the timeout expired, so we need to cancel the original task.
            // Canceling will not affect a task, that is already completed.
            cancellation.Cancel();
            if (completedTask == originalTask)
            {
                // original task completed
                return await originalTask;
            }
            else
            {
                cancelAction?.Invoke(originalTask);
                // timeout
                throw new TimeoutException();
            }
        }
    }
}
