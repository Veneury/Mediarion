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
            CancellationToken cancellationToken) =>
            RequestPipeline.Run<TRequest, TResponse>((TRequest)request, services, cancellationToken);
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

            // A request with nothing to give back answers with Unit, and that is what comes out
            // here too rather than null. Returning null instead reads better and is wrong: code
            // written against the other library and moved over would start seeing a null it
            // never saw before, which is the one thing a drop-in must not do.
            return response;
        }
    }

    internal abstract class NotificationWrapper
    {
        internal abstract Task Handle(
            object notification,
            IServiceProvider services,
            INotificationPublisher publisher,
            CancellationToken cancellationToken);
    }

    internal sealed class NotificationWrapperImpl<TNotification> : NotificationWrapper
        where TNotification : INotification
    {
        internal override Task Handle(
            object notification,
            IServiceProvider services,
            INotificationPublisher publisher,
            CancellationToken cancellationToken) =>
            NotificationPipeline.Run((TNotification)notification, services, publisher, cancellationToken);
    }
}
