using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Mediarion
{
    /// <summary>
    /// A handler that answers with nothing, seen as one that answers with <see cref="Unit"/>.
    /// </summary>
    /// <typeparam name="TRequest">The request it handles.</typeparam>
    /// <remarks>
    /// The pipeline is generic over a response, so a request with no response still needs one to
    /// be generic over. Adapting at registration rather than when the request is sent keeps the
    /// send path free of the question.
    /// <para>
    /// <c>AddMediarion</c> registers one of these for every void handler it finds. It is public
    /// for the other case: an application that registers its handlers by hand, which is what an
    /// application published ahead of time does.
    /// </para>
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class VoidHandlerAdapter<TRequest> : IRequestHandler<TRequest, Unit>
        where TRequest : IRequest
    {
        private readonly IRequestHandler<TRequest> inner;

        /// <summary>Creates the adapter.</summary>
        /// <param name="inner">The handler that answers with nothing.</param>
        public VoidHandlerAdapter(IRequestHandler<TRequest> inner)
        {
            Guard.NotNull(inner, nameof(inner));
            this.inner = inner;
        }

        /// <inheritdoc/>
        public async Task<Unit> Handle(TRequest request, CancellationToken cancellationToken)
        {
            await inner.Handle(request, cancellationToken).ConfigureAwait(false);
            return Unit.Value;
        }
    }
}
