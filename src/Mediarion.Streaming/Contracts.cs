using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Mediarion
{
    /// <summary>
    /// A message whose handler answers with many values, arriving as they are produced.
    /// </summary>
    /// <typeparam name="TResponse">What each value is.</typeparam>
    /// <remarks>
    /// The difference from <see cref="IRequest{TResponse}"/> is when you get the answers. A
    /// request hands back one value once the handler has finished; this hands back each value as
    /// the handler reaches it, so nothing waits for the last one and nothing has to hold them
    /// all at once. Worth it for a result set too big for memory, for a producer slow enough
    /// that starting early matters, and for a sequence that never ends.
    /// </remarks>
    public interface IStreamRequest<out TResponse> : IBaseRequest
    {
    }

    /// <summary>
    /// Handles one streaming request.
    /// </summary>
    /// <typeparam name="TRequest">The request this handles.</typeparam>
    /// <typeparam name="TResponse">What each value is.</typeparam>
    public interface IStreamRequestHandler<in TRequest, out TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        /// <summary>Handles the request.</summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Stops the stream.</param>
        /// <returns>The values, as they are produced.</returns>
        IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The rest of the stream pipeline, as something a behaviour can call.
    /// </summary>
    /// <typeparam name="TResponse">What each value is.</typeparam>
    /// <returns>The values from the next step, or from the handler.</returns>
    [SuppressMessage(
        "Naming",
        "CA1711:Identifiers should not have incorrect suffix",
        Justification = "The name is part of the API this library is a drop-in for.")]
    public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>();

    /// <summary>
    /// Wraps the handling of a streaming request.
    /// </summary>
    /// <typeparam name="TRequest">The request this wraps, or a base of it.</typeparam>
    /// <typeparam name="TResponse">What each value is.</typeparam>
    /// <remarks>
    /// A stream behaviour sees every value on its way out, so it can count them, filter them or
    /// stop early. What it cannot do is treat the request as one thing that succeeded or failed,
    /// because by the time anything has failed some values are already with the caller.
    /// </remarks>
    public interface IStreamPipelineBehavior<in TRequest, TResponse>
        where TRequest : notnull
    {
        /// <summary>Runs around the rest of the pipeline.</summary>
        /// <param name="request">The request.</param>
        /// <param name="next">The rest of the pipeline.</param>
        /// <param name="cancellationToken">Stops the stream.</param>
        /// <returns>The values.</returns>
        [SuppressMessage(
            "Naming",
            "CA1716:Identifiers should not match keywords",
            Justification = "The parameter name is part of the API this library is a drop-in for.")]
        IAsyncEnumerable<TResponse> Handle(
            TRequest request,
            StreamHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Sends a streaming request to the one handler that answers it.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ISender"/> because it lives in a different package, and the core
    /// has no dependencies. The extension method on <see cref="ISender"/> is what keeps the call
    /// site the same as it was.
    /// </remarks>
    public interface IStreamSender
    {
        /// <summary>Sends a streaming request.</summary>
        /// <typeparam name="TResponse">What each value is.</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Stops the stream.</param>
        /// <returns>The values, as they are produced.</returns>
        IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default);

        /// <summary>Sends a streaming request whose type is only known at run time.</summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Stops the stream.</param>
        /// <returns>The values, boxed, as they are produced.</returns>
        IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default);
    }
}
