using System;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents
{
    /// <summary>
    /// The information handed to the override slots of <see cref="ItvErrorBoundary"/>: the captured
    /// <see cref="Exception"/> plus a <see cref="Recover"/> callback that clears the error and re-renders the
    /// protected content. Wire <see cref="Recover"/> to your own retry control when supplying custom markup.
    /// </summary>
    public sealed class ItvErrorContext
    {
        public ItvErrorContext(Exception exception, Action recover)
        {
            Exception = exception;
            Recover = recover;
        }

        /// <summary>The exception the boundary captured from the protected content.</summary>
        public Exception Exception { get; }

        /// <summary>Clears the error and re-renders the protected child content (i.e. tries again).</summary>
        public Action Recover { get; }
    }
}
