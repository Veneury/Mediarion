using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Mediarion
{
    /// <summary>
    /// Runs one request through its behaviours and its handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Public because generated code calls it, and generated code lives in somebody else's
    /// assembly. It is not meant to be called by hand.
    /// </para>
    /// <para>
    /// Both engines go through here. There is no reflection in it — the two type arguments are
    /// closed by whoever calls it, at run time by the mediator and while compiling by the
    /// generator — so sharing it costs the generated path nothing and is what keeps the two from
    /// ever disagreeing about the order behaviours run in.
    /// </para>
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class RequestPipeline
    {
        /// <summary>Runs the request.</summary>
        /// <typeparam name="TRequest">The request type.</typeparam>
        /// <typeparam name="TResponse">What it answers with.</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="services">Where the handler and the behaviours come from.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>The response.</returns>
        public static Task<TResponse> Run<TRequest, TResponse>(
            TRequest request,
            IServiceProvider services,
            CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
        {
            Guard.NotNull(services, nameof(services));

            var handler = (IRequestHandler<TRequest, TResponse>?)
                services.GetService(typeof(IRequestHandler<TRequest, TResponse>));

            if (handler is null)
            {
                throw MediarionException.NoHandler(typeof(TRequest));
            }

            Task<TResponse> Handle(CancellationToken token) => handler.Handle(request, token);

            var behaviours = (IEnumerable<IPipelineBehavior<TRequest, TResponse>>?)
                services.GetService(typeof(IEnumerable<IPipelineBehavior<TRequest, TResponse>>));

            if (behaviours is null)
            {
                return Handle(cancellationToken);
            }

            // Outermost first is how they read in a registration, so the chain is built from the
            // inside out and the list has to be walked backwards to get there.
            var ordered = new List<IPipelineBehavior<TRequest, TResponse>>(behaviours);

            RequestHandlerDelegate<TResponse> next = Handle;

            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                IPipelineBehavior<TRequest, TResponse> behaviour = ordered[i];
                RequestHandlerDelegate<TResponse> inner = next;

                next = token => behaviour.Handle(request, inner, token);
            }

            return next(cancellationToken);
        }
    }

    /// <summary>
    /// Runs one notification through its handlers.
    /// </summary>
    /// <remarks>
    /// Public because generated code calls it. It is not meant to be called by hand.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NotificationPipeline
    {
        /// <summary>Runs the notification.</summary>
        /// <typeparam name="TNotification">The notification type.</typeparam>
        /// <param name="notification">The notification.</param>
        /// <param name="services">Where the handlers come from.</param>
        /// <param name="publisher">How the handlers are run.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that completes when the handlers are done.</returns>
        public static Task Run<TNotification>(
            TNotification notification,
            IServiceProvider services,
            INotificationPublisher publisher,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            Guard.NotNull(services, nameof(services));
            Guard.NotNull(publisher, nameof(publisher));

            var handlers = (IEnumerable<INotificationHandler<TNotification>>?)
                services.GetService(typeof(IEnumerable<INotificationHandler<TNotification>>));

            if (handlers is null)
            {
                return TaskShim.Completed;
            }

            var executors = new List<NotificationHandlerExecutor>();

            foreach (INotificationHandler<TNotification> handler in handlers)
            {
                INotificationHandler<TNotification> captured = handler;
                executors.Add((raised, token) => captured.Handle((TNotification)raised, token));
            }

            return executors.Count == 0
                ? TaskShim.Completed
                : publisher.Publish(executors, notification!, cancellationToken);
        }
    }

    internal static class TaskShim
    {
        internal static Task Completed { get; } = Task.FromResult(0);
    }
}
