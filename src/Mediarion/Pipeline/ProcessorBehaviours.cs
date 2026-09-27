using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mediarion.Pipeline
{
    /// <summary>
    /// Runs every <see cref="IRequestPreProcessor{TRequest}"/> for the request, then the rest of
    /// the pipeline.
    /// </summary>
    /// <typeparam name="TRequest">The request.</typeparam>
    /// <typeparam name="TResponse">What the request answers with.</typeparam>
    /// <remarks>
    /// A pre-processor is a behaviour that only wants the "before" half, without having to
    /// remember to call the next step. This is the behaviour that gives it that.
    /// </remarks>
    public sealed class RequestPreProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IEnumerable<IRequestPreProcessor<TRequest>> processors;

        /// <summary>Creates the behaviour.</summary>
        /// <param name="processors">The pre-processors registered for this request.</param>
        public RequestPreProcessorBehavior(IEnumerable<IRequestPreProcessor<TRequest>> processors)
        {
            Guard.NotNull(processors, nameof(processors));
            this.processors = processors;
        }

        /// <inheritdoc/>
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            Guard.NotNull(next, nameof(next));

            foreach (IRequestPreProcessor<TRequest> processor in processors)
            {
                await processor.Process(request, cancellationToken).ConfigureAwait(false);
            }

            return await next(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs the rest of the pipeline, then every <see cref="IRequestPostProcessor{TRequest, TResponse}"/>
    /// for the request, with the response the handler produced.
    /// </summary>
    /// <typeparam name="TRequest">The request.</typeparam>
    /// <typeparam name="TResponse">What the request answers with.</typeparam>
    public sealed class RequestPostProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IEnumerable<IRequestPostProcessor<TRequest, TResponse>> processors;

        /// <summary>Creates the behaviour.</summary>
        /// <param name="processors">The post-processors registered for this request.</param>
        public RequestPostProcessorBehavior(IEnumerable<IRequestPostProcessor<TRequest, TResponse>> processors)
        {
            Guard.NotNull(processors, nameof(processors));
            this.processors = processors;
        }

        /// <inheritdoc/>
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            Guard.NotNull(next, nameof(next));

            TResponse response = await next(cancellationToken).ConfigureAwait(false);

            foreach (IRequestPostProcessor<TRequest, TResponse> processor in processors)
            {
                await processor.Process(request, response, cancellationToken).ConfigureAwait(false);
            }

            return response;
        }
    }
}
