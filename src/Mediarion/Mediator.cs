using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Mediarion.Execution;
using Mediarion.NotificationPublishers;

namespace Mediarion
{
    /// <summary>
    /// Sends requests to their handler and publishes notifications to theirs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A request arrives as an <c>IRequest&lt;TResponse&gt;</c>, so the response type is known
    /// and the request type is not — and the handler and the behaviours are generic over the
    /// request type. Closing those generics needs the run-time type, which is why this is built
    /// on reflection and why it is marked as such: an application published ahead of time gets a
    /// warning from the compiler rather than a failure on the first request.
    /// </para>
    /// <para>
    /// The closing happens once per request type and the result is kept, so the reflection is
    /// paid on the first send and never again.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode("The mediator closes handler and behaviour generics over the request type at run time.")]
    [RequiresDynamicCode("The mediator closes handler and behaviour generics over the request type at run time.")]
    public class Mediator : IMediator
    {
        private static readonly ConcurrentDictionary<Type, RequestWrapper> Boxed =
            new ConcurrentDictionary<Type, RequestWrapper>();

        private static readonly ConcurrentDictionary<Type, object> Typed =
            new ConcurrentDictionary<Type, object>();

        private static readonly ConcurrentDictionary<Type, NotificationWrapper> Notifications =
            new ConcurrentDictionary<Type, NotificationWrapper>();

        private readonly IServiceProvider services;
        private readonly INotificationPublisher publisher;

        /// <summary>Creates a mediator that resolves its handlers from the given provider.</summary>
        /// <param name="services">Where handlers and behaviours come from.</param>
        public Mediator(IServiceProvider services)
            : this(services, new ForeachAwaitPublisher())
        {
        }

        /// <summary>Creates a mediator with a publishing strategy of its own.</summary>
        /// <param name="services">Where handlers and behaviours come from.</param>
        /// <param name="publisher">How the handlers of one notification are run.</param>
        public Mediator(IServiceProvider services, INotificationPublisher publisher)
        {
            Guard.NotNull(services, nameof(services));
            Guard.NotNull(publisher, nameof(publisher));

            this.services = services;
            this.publisher = publisher;
        }

        /// <inheritdoc/>
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            Guard.NotNull(request, nameof(request));

            Type requestType = request.GetType();

            var wrapper = (RequestWrapper<TResponse>)Typed.GetOrAdd(
                requestType,
                static type => Activator.CreateInstance(
                    typeof(RequestWrapperImpl<,>).MakeGenericType(type, typeof(TResponse)))!);

            return wrapper.Handle(request, services, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            Guard.NotNull(request, nameof(request));

            Type requestType = request.GetType();

            RequestWrapper wrapper = Boxed.GetOrAdd(
                requestType,
                static type =>
                {
                    Type responseType = ResponseOf(type) ?? throw MediarionException.NotARequest(type);

                    return (RequestWrapper)Activator.CreateInstance(
                        typeof(BoxedRequestWrapperImpl<,>).MakeGenericType(type, responseType))!;
                });

            return wrapper.Handle(request, services, cancellationToken);
        }

        /// <inheritdoc/>
        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Guard.NotNull(notification, nameof(notification));

            return PublishCore(notification!, notification!.GetType(), cancellationToken);
        }

        /// <inheritdoc/>
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Guard.NotNull(notification, nameof(notification));

            if (notification is not INotification)
            {
                throw MediarionException.NotANotification(notification.GetType());
            }

            return PublishCore(notification, notification.GetType(), cancellationToken);
        }

        /// <summary>The response type a request answers with, or null when it is not a request.</summary>
        internal static Type? ResponseOf(Type requestType)
        {
            foreach (Type contract in requestType.GetInterfaces())
            {
                if (contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == typeof(IRequest<>))
                {
                    return contract.GetGenericArguments()[0];
                }
            }

            return null;
        }

        private Task PublishCore(object notification, Type notificationType, CancellationToken cancellationToken)
        {
            NotificationWrapper wrapper = Notifications.GetOrAdd(
                notificationType,
                static type => (NotificationWrapper)Activator.CreateInstance(
                    typeof(NotificationWrapperImpl<>).MakeGenericType(type))!);

            return wrapper.Handle(notification, services, publisher, cancellationToken);
        }
    }
}
