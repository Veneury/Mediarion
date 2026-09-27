using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mediarion.Execution
{
    /// <summary>
    /// A request whose type is only known at run time, reached without naming it.
    /// </summary>
    /// <remarks>
    /// <see cref="ISender.Send{TResponse}"/> is handed an <c>IRequest&lt;TResponse&gt;</c> and
    /// knows the response type but not the request type, which is the one the handler and the
    /// behaviours are generic over. One of these is built per request type, once, and kept.
    /// </remarks>
    internal abstract class RequestWrapper<TResponse>
    {
        internal abstract Task<TResponse> Handle(
            object request,
            IServiceProvider services,
            CancellationToken cancellationToken);
    }

    /// <summary>The same thing again for a caller that does not know the response type either.</summary>
    internal abstract class RequestWrapper
    {
        internal abstract Task<object?> Handle(
            object request,
            IServiceProvider services,
            CancellationToken cancellationToken);
    }

    internal sealed class RequestWrapperImpl<TRequest, TResponse> : RequestWrapper<TResponse>
        where TRequest : IRequest<TResponse>
    {
        internal override Task<TResponse> Handle(
            object request,
            IServiceProvider services,
            CancellationToken cancellationToken)
        {
            var handler = (IRequestHandler<TRequest, TResponse>?)
                services.GetService(typeof(IRequestHandler<TRequest, TResponse>));

            if (handler is null)
            {
                throw MediarionException.NoHandler(typeof(TRequest));
            }

            var typed = (TRequest)request;

            Task<TResponse> Handle(CancellationToken token) =>
                handler.Handle(typed, token);

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

                next = token => behaviour.Handle(typed, inner, token);
            }

            return next(cancellationToken);
        }
    }

    internal sealed class BoxedRequestWrapperImpl<TRequest, TResponse> : RequestWrapper
        where TRequest : IRequest<TResponse>
    {
        internal override async Task<object?> Handle(
            object request,
            IServiceProvider services,
            CancellationToken cancellationToken)
        {
            var inner = new RequestWrapperImpl<TRequest, TResponse>();
            TResponse response = await inner.Handle(request, services, cancellationToken).ConfigureAwait(false);

            return response is Unit ? null : response;
        }
    }

    /// <summary>
    /// A handler that answers with nothing, seen as one that answers with <see cref="Unit"/>.
    /// </summary>
    /// <remarks>
    /// The pipeline is generic over a response, so a request with no response still needs one to
    /// be generic over. Adapting at registration rather than at send time keeps the send path
    /// free of the question.
    /// </remarks>
    internal sealed class VoidHandlerAdapter<TRequest> : IRequestHandler<TRequest, Unit>
        where TRequest : IRequest
    {
        private readonly IRequestHandler<TRequest> inner;

        public VoidHandlerAdapter(IRequestHandler<TRequest> inner)
        {
            this.inner = inner;
        }

        public async Task<Unit> Handle(TRequest request, CancellationToken cancellationToken)
        {
            await inner.Handle(request, cancellationToken).ConfigureAwait(false);
            return Unit.Value;
        }
    }

    internal abstract class NotificationWrapper
    {
        internal abstract IReadOnlyList<NotificationHandlerExecutor> Handlers(IServiceProvider services);
    }

    internal sealed class NotificationWrapperImpl<TNotification> : NotificationWrapper
        where TNotification : INotification
    {
        internal override IReadOnlyList<NotificationHandlerExecutor> Handlers(IServiceProvider services)
        {
            var handlers = (IEnumerable<INotificationHandler<TNotification>>?)
                services.GetService(typeof(IEnumerable<INotificationHandler<TNotification>>));

            if (handlers is null)
            {
                return Array.Empty<NotificationHandlerExecutor>();
            }

            var executors = new List<NotificationHandlerExecutor>();

            foreach (INotificationHandler<TNotification> handler in handlers)
            {
                INotificationHandler<TNotification> captured = handler;
                executors.Add((notification, token) => captured.Handle((TNotification)notification, token));
            }

            return executors;
        }
    }
}
