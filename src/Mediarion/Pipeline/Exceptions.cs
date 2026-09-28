using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Mediarion.Pipeline
{
    /// <summary>
    /// Whether an exception has been dealt with, and what to answer with instead.
    /// </summary>
    /// <typeparam name="TResponse">What the request answers with.</typeparam>
    public class RequestExceptionHandlerState<TResponse>
    {
        /// <summary>Gets whether a handler has dealt with the exception.</summary>
        public bool Handled { get; private set; }

        /// <summary>Gets the response to answer with instead, once one has been set.</summary>
        public TResponse? Response { get; private set; }

        /// <summary>
        /// Says the exception has been dealt with, and what the request answers with instead of
        /// throwing.
        /// </summary>
        /// <param name="response">The response.</param>
        public void SetHandled(TResponse response)
        {
            Handled = true;
            Response = response;
        }
    }

    /// <summary>
    /// Deals with one kind of exception out of one kind of request, and may answer in its place.
    /// </summary>
    /// <typeparam name="TRequest">The request.</typeparam>
    /// <typeparam name="TResponse">What the request answers with.</typeparam>
    /// <typeparam name="TException">The exception this deals with, or a base of it.</typeparam>
    public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
        where TRequest : notnull
        where TException : Exception
    {
        /// <summary>Deals with the exception.</summary>
        /// <param name="request">The request that was being handled.</param>
        /// <param name="exception">What was thrown.</param>
        /// <param name="state">Where to say it was dealt with, and with what.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that completes when this handler is done.</returns>
        Task Handle(
            TRequest request,
            TException exception,
            RequestExceptionHandlerState<TResponse> state,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Deals with any exception out of one kind of request.
    /// </summary>
    /// <typeparam name="TRequest">The request.</typeparam>
    /// <typeparam name="TResponse">What the request answers with.</typeparam>
    public interface IRequestExceptionHandler<in TRequest, TResponse>
        : IRequestExceptionHandler<TRequest, TResponse, Exception>
        where TRequest : notnull
    {
    }

    /// <summary>
    /// Runs when one kind of request throws, and cannot stop it throwing.
    /// </summary>
    /// <typeparam name="TRequest">The request.</typeparam>
    /// <typeparam name="TException">The exception this runs for, or a base of it.</typeparam>
    /// <remarks>
    /// For the things that should happen on a failure without changing what the caller sees:
    /// a metric, a log line, a message on a queue. The exception is rethrown afterwards either
    /// way, which is the difference between this and a handler.
    /// </remarks>
    public interface IRequestExceptionAction<in TRequest, in TException>
        where TRequest : notnull
        where TException : Exception
    {
        /// <summary>Runs for the exception.</summary>
        /// <param name="request">The request that was being handled.</param>
        /// <param name="exception">What was thrown.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that completes when this action is done.</returns>
        Task Execute(TRequest request, TException exception, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Runs when one kind of request throws anything at all.
    /// </summary>
    /// <typeparam name="TRequest">The request.</typeparam>
    public interface IRequestExceptionAction<in TRequest> : IRequestExceptionAction<TRequest, Exception>
        where TRequest : notnull
    {
    }

    /// <summary>
    /// A handler for one exception type, seen as one that takes any exception.
    /// </summary>
    /// <typeparam name="TRequest">The request.</typeparam>
    /// <typeparam name="TResponse">What the request answers with.</typeparam>
    /// <typeparam name="TException">The exception the inner handler deals with.</typeparam>
    /// <remarks>
    /// <para>
    /// Choosing a handler by the exception's type is the one thing here that would need
    /// reflection: the behaviour catches an <see cref="Exception"/> and has to find the handlers
    /// registered for whatever it turned out to be. Closing a generic over a type learned at run
    /// time is exactly what an application published ahead of time cannot do.
    /// </para>
    /// <para>
    /// So the choosing is moved to registration, where the exception type is known — by the scan
    /// or by the generator — and what the behaviour resolves is one list of handlers that all
    /// take <see cref="Exception"/>. Each of these tests the type itself and stands aside when it
    /// does not match, which is a type test rather than a type constructed on the spot.
    /// </para>
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ExceptionHandlerAdapter<TRequest, TResponse, TException>
        : IRequestExceptionHandler<TRequest, TResponse, Exception>
        where TRequest : notnull
        where TException : Exception
    {
        private readonly IRequestExceptionHandler<TRequest, TResponse, TException> inner;

        /// <summary>Creates the adapter.</summary>
        /// <param name="inner">The handler that deals with one exception type.</param>
        public ExceptionHandlerAdapter(IRequestExceptionHandler<TRequest, TResponse, TException> inner)
        {
            Guard.NotNull(inner, nameof(inner));
            this.inner = inner;
        }

        /// <inheritdoc/>
        public Task Handle(
            TRequest request,
            Exception exception,
            RequestExceptionHandlerState<TResponse> state,
            CancellationToken cancellationToken) =>
            exception is TException typed
                ? inner.Handle(request, typed, state, cancellationToken)
                : TaskShim.Completed;
    }

    /// <summary>
    /// An action for one exception type, seen as one that takes any exception.
    /// </summary>
    /// <typeparam name="TRequest">The request.</typeparam>
    /// <typeparam name="TException">The exception the inner action runs for.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ExceptionActionAdapter<TRequest, TException>
        : IRequestExceptionAction<TRequest, Exception>
        where TRequest : notnull
        where TException : Exception
    {
        private readonly IRequestExceptionAction<TRequest, TException> inner;

        /// <summary>Creates the adapter.</summary>
        /// <param name="inner">The action that runs for one exception type.</param>
        public ExceptionActionAdapter(IRequestExceptionAction<TRequest, TException> inner)
        {
            Guard.NotNull(inner, nameof(inner));
            this.inner = inner;
        }

        /// <inheritdoc/>
        public Task Execute(TRequest request, Exception exception, CancellationToken cancellationToken) =>
            exception is TException typed
                ? inner.Execute(request, typed, cancellationToken)
                : TaskShim.Completed;
    }
}
