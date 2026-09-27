using System;

namespace Mediarion
{
    /// <summary>
    /// Something went wrong that is the mediator's business rather than a handler's.
    /// </summary>
    /// <remarks>
    /// An exception thrown by a handler is not wrapped: it comes out as the handler threw it,
    /// with its stack intact. Wrapping would make every catch in the application reach through
    /// a layer that told it nothing.
    /// </remarks>
    public class MediarionException : Exception
    {
        /// <summary>Creates the exception.</summary>
        public MediarionException()
        {
        }

        /// <summary>Creates the exception with a message.</summary>
        /// <param name="message">What went wrong.</param>
        public MediarionException(string message)
            : base(message)
        {
        }

        /// <summary>Creates the exception with a message and the failure underneath it.</summary>
        /// <param name="message">What went wrong.</param>
        /// <param name="innerException">The failure underneath.</param>
        public MediarionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        internal static MediarionException NoHandler(Type requestType) =>
            new MediarionException(
                "No handler is registered for " + requestType.Name + ". Register an " +
                "IRequestHandler for it, or add the assembly it lives in with " +
                "AddMediarion(typeof(" + requestType.Name + ").Assembly).");

        internal static MediarionException NotARequest(Type type) =>
            new MediarionException(
                type.Name + " is not a request. A request implements IRequest or " +
                "IRequest<TResponse>.");

        internal static MediarionException NotANotification(Type type) =>
            new MediarionException(
                type.Name + " is not a notification. A notification implements INotification.");
    }
}
