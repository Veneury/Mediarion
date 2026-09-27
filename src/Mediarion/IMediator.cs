using System.Threading;
using System.Threading.Tasks;

namespace Mediarion
{
    /// <summary>
    /// Sends a request to the one handler that answers it.
    /// </summary>
    public interface ISender
    {
        /// <summary>Sends a request and waits for its answer.</summary>
        /// <typeparam name="TResponse">What the request answers with.</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>The response.</returns>
        Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

        /// <summary>Sends a request whose type is only known at run time.</summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>The response, boxed. A request with nothing to give back answers with <see cref="Unit"/>.</returns>
        Task<object?> Send(object request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Publishes a notification to every handler that takes it.
    /// </summary>
    public interface IPublisher
    {
        /// <summary>Publishes a notification.</summary>
        /// <typeparam name="TNotification">The notification type.</typeparam>
        /// <param name="notification">The notification.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that completes when the handlers are done.</returns>
        Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification;

        /// <summary>Publishes a notification whose type is only known at run time.</summary>
        /// <param name="notification">The notification.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that completes when the handlers are done.</returns>
        Task Publish(object notification, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Both halves of the mediator: sending requests and publishing notifications.
    /// </summary>
    /// <remarks>
    /// Most code wants one of the two. Taking <see cref="ISender"/> or <see cref="IPublisher"/>
    /// instead of this says which, and makes a test double smaller.
    /// </remarks>
    public interface IMediator : ISender, IPublisher
    {
    }
}
