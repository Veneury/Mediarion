using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Mediarion
{
    /// <summary>
    /// A mediator that can stream as well as send and publish.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It derives from <see cref="Mediator"/> rather than replacing it, so registering this
    /// package changes what <c>ISender</c> resolves to and nothing else: sending and publishing
    /// behave exactly as they did.
    /// </para>
    /// <para>
    /// A streaming request arrives as an <c>IStreamRequest&lt;TResponse&gt;</c>, so the response
    /// type is known and the request type is not, and the handler is generic over the request
    /// type. Closing that needs the run-time type, which is why this is marked as needing
    /// dynamic code — and why the generator writes the same dispatch as a switch for
    /// applications published ahead of time.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode("The mediator closes handler and behaviour generics over the request type at run time.")]
    [RequiresDynamicCode("The mediator closes handler and behaviour generics over the request type at run time.")]
    public class StreamingMediator : Mediator, IStreamSender
    {
        private static readonly ConcurrentDictionary<Type, object> Typed =
            new ConcurrentDictionary<Type, object>();

        private static readonly ConcurrentDictionary<Type, StreamWrapper> Boxed =
            new ConcurrentDictionary<Type, StreamWrapper>();

        private readonly IServiceProvider services;

        /// <summary>Creates a mediator that resolves its handlers from the given provider.</summary>
        /// <param name="services">Where handlers and behaviours come from.</param>
        public StreamingMediator(IServiceProvider services)
            : base(services)
        {
            this.services = services;
        }

        /// <summary>Creates a mediator with a publishing strategy of its own.</summary>
        /// <param name="services">Where handlers and behaviours come from.</param>
        /// <param name="publisher">How the handlers of one notification are run.</param>
        public StreamingMediator(IServiceProvider services, INotificationPublisher publisher)
            : base(services, publisher)
        {
            this.services = services;
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            Guard.NotNull(request, nameof(request));

            var wrapper = (StreamWrapper<TResponse>)Typed.GetOrAdd(
                request.GetType(),
                static type => Activator.CreateInstance(
                    typeof(StreamWrapperImpl<,>).MakeGenericType(type, typeof(TResponse)))!);

            return wrapper.Handle(request, services, cancellationToken);
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            Guard.NotNull(request, nameof(request));

            StreamWrapper wrapper = Boxed.GetOrAdd(
                request.GetType(),
                static type =>
                {
                    Type response = StreamResponseOf(type) ?? throw StreamErrors.NotAStreamRequest(type);

                    return (StreamWrapper)Activator.CreateInstance(
                        typeof(BoxedStreamWrapperImpl<,>).MakeGenericType(type, response))!;
                });

            return wrapper.Handle(request, services, cancellationToken);
        }

        /// <summary>What each value of a streaming request is, or null when it is not one.</summary>
        internal static Type? StreamResponseOf(Type requestType)
        {
            foreach (Type contract in requestType.GetInterfaces())
            {
                if (contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == typeof(IStreamRequest<>))
                {
                    return contract.GetGenericArguments()[0];
                }
            }

            return null;
        }
    }

    internal abstract class StreamWrapper<TResponse>
    {
        internal abstract IAsyncEnumerable<TResponse> Handle(
            object request,
            IServiceProvider services,
            CancellationToken cancellationToken);
    }

    internal abstract class StreamWrapper
    {
        internal abstract IAsyncEnumerable<object?> Handle(
            object request,
            IServiceProvider services,
            CancellationToken cancellationToken);
    }

    internal sealed class StreamWrapperImpl<TRequest, TResponse> : StreamWrapper<TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        internal override IAsyncEnumerable<TResponse> Handle(
            object request,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
            StreamPipeline.Run<TRequest, TResponse>((TRequest)request, services, cancellationToken);
    }

    internal sealed class BoxedStreamWrapperImpl<TRequest, TResponse> : StreamWrapper
        where TRequest : IStreamRequest<TResponse>
    {
        internal override IAsyncEnumerable<object?> Handle(
            object request,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
            StreamPipeline.RunBoxed<TRequest, TResponse>((TRequest)request, services, cancellationToken);
    }
}
