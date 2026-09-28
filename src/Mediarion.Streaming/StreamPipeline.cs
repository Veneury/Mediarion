using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mediarion
{
    /// <summary>
    /// Runs one streaming request through its behaviours and its handler.
    /// </summary>
    /// <remarks>
    /// Public because generated code calls it, and generated code lives in somebody else's
    /// assembly. It is not meant to be called by hand. There is no reflection in it: both type
    /// arguments are closed by whoever calls it, at run time by the mediator and while compiling
    /// by the generator.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class StreamPipeline
    {
        /// <summary>Runs the request.</summary>
        /// <typeparam name="TRequest">The request type.</typeparam>
        /// <typeparam name="TResponse">What each value is.</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="services">Where the handler and the behaviours come from.</param>
        /// <param name="cancellationToken">Stops the stream.</param>
        /// <returns>The values, as they are produced.</returns>
        public static IAsyncEnumerable<TResponse> Run<TRequest, TResponse>(
            TRequest request,
            IServiceProvider services,
            CancellationToken cancellationToken)
            where TRequest : IStreamRequest<TResponse>
        {
            Guard.NotNull(services, nameof(services));

            var handler = (IStreamRequestHandler<TRequest, TResponse>?)
                services.GetService(typeof(IStreamRequestHandler<TRequest, TResponse>));

            if (handler is null)
            {
                throw StreamErrors.NoHandler(typeof(TRequest));
            }

            var behaviours = (IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>>?)
                services.GetService(typeof(IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>>));

            IReadOnlyList<IStreamPipelineBehavior<TRequest, TResponse>> ordered =
                behaviours as IReadOnlyList<IStreamPipelineBehavior<TRequest, TResponse>>
                ?? (behaviours is null
                    ? Array.Empty<IStreamPipelineBehavior<TRequest, TResponse>>()
                    : new List<IStreamPipelineBehavior<TRequest, TResponse>>(behaviours));

            if (ordered.Count == 0)
            {
                return handler.Handle(request, cancellationToken);
            }

            IAsyncEnumerable<TResponse> Handle() => handler.Handle(request, cancellationToken);

            // Outermost first is how they read in a registration, so the chain is built from the
            // inside out and the list has to be walked backwards to get there.
            StreamHandlerDelegate<TResponse> next = Handle;

            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                IStreamPipelineBehavior<TRequest, TResponse> behaviour = ordered[i];
                StreamHandlerDelegate<TResponse> inner = next;

                next = () => behaviour.Handle(request, inner, cancellationToken);
            }

            return next();
        }

        /// <summary>Runs the request and boxes each value, for a caller that knows neither type.</summary>
        /// <typeparam name="TRequest">The request type.</typeparam>
        /// <typeparam name="TResponse">What each value is.</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="services">Where the handler and the behaviours come from.</param>
        /// <param name="cancellationToken">Stops the stream.</param>
        /// <returns>The values, boxed.</returns>
        public static async IAsyncEnumerable<object?> RunBoxed<TRequest, TResponse>(
            TRequest request,
            IServiceProvider services,
            [EnumeratorCancellation] CancellationToken cancellationToken)
            where TRequest : IStreamRequest<TResponse>
        {
            await foreach (TResponse value in Run<TRequest, TResponse>(request, services, cancellationToken)
                .ConfigureAwait(false))
            {
                yield return value;
            }
        }
    }

    internal static class StreamErrors
    {
        internal static MediarionException NoHandler(Type requestType) =>
            new MediarionException(
                "No streaming handler is registered for " + requestType.Name + ". Register an " +
                "IStreamRequestHandler for it, or add the assembly it lives in with " +
                "AddMediarionStreaming(typeof(" + requestType.Name + ").Assembly).");

        internal static MediarionException NotAStreamRequest(Type type) =>
            new MediarionException(
                type.Name + " is not a streaming request. One implements IStreamRequest<TResponse>.");
    }
}
