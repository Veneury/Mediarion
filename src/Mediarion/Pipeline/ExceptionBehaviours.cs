using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mediarion.Pipeline
{
    /// <summary>
    /// Gives the exception handlers a chance to deal with what the request threw, and to answer
    /// in its place.
    /// </summary>
    /// <typeparam name="TRequest">The request.</typeparam>
    /// <typeparam name="TResponse">What the request answers with.</typeparam>
    /// <remarks>
    /// The first handler to say it dealt with the exception stops the rest, and its response is
    /// what the caller gets. If none does, the exception is rethrown as it was thrown: the stack
    /// is not touched and nothing is wrapped, so a <c>catch</c> further out still works.
    /// </remarks>
    public sealed class RequestExceptionProcessorBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IEnumerable<IRequestExceptionHandler<TRequest, TResponse, Exception>> handlers;

        /// <summary>Creates the behaviour.</summary>
        /// <param name="handlers">The exception handlers registered for this request.</param>
        public RequestExceptionProcessorBehavior(
            IEnumerable<IRequestExceptionHandler<TRequest, TResponse, Exception>> handlers)
        {
            Guard.NotNull(handlers, nameof(handlers));
            this.handlers = handlers;
        }

        /// <inheritdoc/>
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            Guard.NotNull(next, nameof(next));

            try
            {
                return await next(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                var state = new RequestExceptionHandlerState<TResponse>();

                foreach (IRequestExceptionHandler<TRequest, TResponse, Exception> handler in handlers)
                {
                    await handler.Handle(request, error, state, cancellationToken).ConfigureAwait(false);

                    if (state.Handled)
                    {
                        break;
                    }
                }

                if (!state.Handled)
                {
                    throw;
                }

                return state.Response!;
            }
        }
    }

    /// <summary>
    /// Runs the exception actions for what the request threw, and lets it carry on throwing.
    /// </summary>
    /// <typeparam name="TRequest">The request.</typeparam>
    /// <typeparam name="TResponse">What the request answers with.</typeparam>
    /// <remarks>
    /// Every action runs, and the exception is rethrown afterwards whatever they did. An action
    /// that throws replaces the original exception, which is the usual hazard of doing work on
    /// the way out and is left alone rather than swallowed: hiding the failure of a logger is
    /// worse than losing the exception it was logging.
    /// </remarks>
    public sealed class RequestExceptionActionProcessorBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IEnumerable<IRequestExceptionAction<TRequest, Exception>> actions;

        /// <summary>Creates the behaviour.</summary>
        /// <param name="actions">The exception actions registered for this request.</param>
        public RequestExceptionActionProcessorBehavior(
            IEnumerable<IRequestExceptionAction<TRequest, Exception>> actions)
        {
            Guard.NotNull(actions, nameof(actions));
            this.actions = actions;
        }

        /// <inheritdoc/>
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            Guard.NotNull(next, nameof(next));

            try
            {
                return await next(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                foreach (IRequestExceptionAction<TRequest, Exception> action in actions)
                {
                    await action.Execute(request, error, cancellationToken).ConfigureAwait(false);
                }

                throw;
            }
        }
    }
}
