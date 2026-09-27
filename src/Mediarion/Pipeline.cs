using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Mediarion
{
    /// <summary>
    /// The rest of the pipeline, as something a behaviour can call.
    /// </summary>
    /// <typeparam name="TResponse">What the request answers with.</typeparam>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The response from the next step, or from the handler.</returns>
    [SuppressMessage(
        "Naming",
        "CA1711:Identifiers should not have incorrect suffix",
        Justification = "The name is part of the API this library is a drop-in for. Renaming it " +
                        "would mean editing every behaviour written against the other one.")]
    public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps the handling of a request: logging, validation, a transaction, a retry.
    /// </summary>
    /// <typeparam name="TRequest">The request this wraps, or a base of it.</typeparam>
    /// <typeparam name="TResponse">What the request answers with.</typeparam>
    /// <remarks>
    /// Behaviours run in the order they are registered, outermost first, and the handler is
    /// innermost. A behaviour that does not call the next step short-circuits everything below
    /// it, which is how a cache or a guard is written.
    /// </remarks>
    public interface IPipelineBehavior<in TRequest, TResponse>
        where TRequest : notnull
    {
        /// <summary>Runs around the rest of the pipeline.</summary>
        /// <param name="request">The request.</param>
        /// <param name="next">The rest of the pipeline.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>The response.</returns>
        [SuppressMessage(
            "Naming",
            "CA1716:Identifiers should not match keywords",
            Justification = "The parameter name is part of the API this library is a drop-in for.")]
        Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken);
    }
}
