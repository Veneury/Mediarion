using System.Threading;
using System.Threading.Tasks;

namespace Mediarion.Pipeline
{
    /// <summary>
    /// Runs before the handler, for a request it does not answer.
    /// </summary>
    /// <typeparam name="TRequest">The request this runs before.</typeparam>
    public interface IRequestPreProcessor<in TRequest>
        where TRequest : notnull
    {
        /// <summary>Runs before the handler.</summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that completes when this is done.</returns>
        Task Process(TRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Runs after the handler, with the response it produced.
    /// </summary>
    /// <typeparam name="TRequest">The request this runs after.</typeparam>
    /// <typeparam name="TResponse">What the request answered with.</typeparam>
    public interface IRequestPostProcessor<in TRequest, in TResponse>
        where TRequest : notnull
    {
        /// <summary>Runs after the handler.</summary>
        /// <param name="request">The request.</param>
        /// <param name="response">What the handler answered with.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that completes when this is done.</returns>
        Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
    }
}
