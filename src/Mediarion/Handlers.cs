using System.Threading;
using System.Threading.Tasks;

namespace Mediarion
{
    /// <summary>
    /// Handles one request and answers with <typeparamref name="TResponse"/>.
    /// </summary>
    /// <typeparam name="TRequest">The request this handles.</typeparam>
    /// <typeparam name="TResponse">What it answers with.</typeparam>
    public interface IRequestHandler<in TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>Handles the request.</summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>The response.</returns>
        Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Handles one request that has nothing to give back.
    /// </summary>
    /// <typeparam name="TRequest">The request this handles.</typeparam>
    public interface IRequestHandler<in TRequest>
        where TRequest : IRequest
    {
        /// <summary>Handles the request.</summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        Task Handle(TRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Handles a notification. Any number of these may exist for one notification.
    /// </summary>
    /// <typeparam name="TNotification">The notification this handles.</typeparam>
    public interface INotificationHandler<in TNotification>
        where TNotification : INotification
    {
        /// <summary>Handles the notification.</summary>
        /// <param name="notification">The notification.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that completes when this handler is done.</returns>
        Task Handle(TNotification notification, CancellationToken cancellationToken);
    }
}
