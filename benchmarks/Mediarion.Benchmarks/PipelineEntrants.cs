using System.Threading;
using System.Threading.Tasks;

// A request with four behaviours around it, per library. The namespaces are deliberately not
// named after their libraries: a namespace in the enclosing chain beats a using directive.
namespace Mediarion.Benchmarks.ByHand
{
    public sealed class Work
    {
        public int Value { get; set; }
    }

    /// <remarks>
    /// The baseline: the same four steps as the behaviours below, written as a program would
    /// write them without a mediator — four calls, no indirection, no delegate chain. The
    /// mediator has to reach the same place through a pipeline it builds, and the gap between
    /// them is what the benchmark is for.
    /// </remarks>
    public sealed class Piped
    {
        public Task<int> Run(Work request, CancellationToken cancellationToken) =>
            First(request, cancellationToken);

        private Task<int> First(Work request, CancellationToken cancellationToken) =>
            Second(request, cancellationToken);

        private Task<int> Second(Work request, CancellationToken cancellationToken) =>
            Third(request, cancellationToken);

        private Task<int> Third(Work request, CancellationToken cancellationToken) =>
            Fourth(request, cancellationToken);

        private Task<int> Fourth(Work request, CancellationToken cancellationToken) =>
            Handle(request, cancellationToken);

        private static Task<int> Handle(Work request, CancellationToken cancellationToken) =>
            Task.FromResult(request.Value + 1);
    }
}

namespace Mediarion.Benchmarks.ForMediarion
{
    public sealed class Work : global::Mediarion.IRequest<int>
    {
        public int Value { get; set; }
    }

    public sealed class WorkHandler : global::Mediarion.IRequestHandler<Work, int>
    {
        public Task<int> Handle(Work request, CancellationToken cancellationToken) =>
            Task.FromResult(request.Value + 1);
    }

    public sealed class Step1<TRequest, TResponse> : global::Mediarion.IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(
            TRequest request,
            global::Mediarion.RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }

    public sealed class Step2<TRequest, TResponse> : global::Mediarion.IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(
            TRequest request,
            global::Mediarion.RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }

    public sealed class Step3<TRequest, TResponse> : global::Mediarion.IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(
            TRequest request,
            global::Mediarion.RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }

    public sealed class Step4<TRequest, TResponse> : global::Mediarion.IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(
            TRequest request,
            global::Mediarion.RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }
}

namespace Mediarion.Benchmarks.ForMediatR
{
    public sealed class Work : global::MediatR.IRequest<int>
    {
        public int Value { get; set; }
    }

    public sealed class WorkHandler : global::MediatR.IRequestHandler<Work, int>
    {
        public Task<int> Handle(Work request, CancellationToken cancellationToken) =>
            Task.FromResult(request.Value + 1);
    }

    public sealed class Step1<TRequest, TResponse> : global::MediatR.IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(
            TRequest request,
            global::MediatR.RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }

    public sealed class Step2<TRequest, TResponse> : global::MediatR.IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(
            TRequest request,
            global::MediatR.RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }

    public sealed class Step3<TRequest, TResponse> : global::MediatR.IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(
            TRequest request,
            global::MediatR.RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }

    public sealed class Step4<TRequest, TResponse> : global::MediatR.IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(
            TRequest request,
            global::MediatR.RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }
}

namespace Mediarion.Benchmarks.ForMediator
{
    public sealed class Work : global::Mediator.IRequest<int>
    {
        public int Value { get; set; }
    }

    public sealed class WorkHandler : global::Mediator.IRequestHandler<Work, int>
    {
        public ValueTask<int> Handle(Work request, CancellationToken cancellationToken) =>
            new ValueTask<int>(request.Value + 1);
    }

    public sealed class Step1<TRequest, TResponse> : global::Mediator.IPipelineBehavior<TRequest, TResponse>
        where TRequest : global::Mediator.IMessage
    {
        public ValueTask<TResponse> Handle(
            TRequest request,
            global::Mediator.MessageHandlerDelegate<TRequest, TResponse> next,
            CancellationToken cancellationToken) => next(request, cancellationToken);
    }

    public sealed class Step2<TRequest, TResponse> : global::Mediator.IPipelineBehavior<TRequest, TResponse>
        where TRequest : global::Mediator.IMessage
    {
        public ValueTask<TResponse> Handle(
            TRequest request,
            global::Mediator.MessageHandlerDelegate<TRequest, TResponse> next,
            CancellationToken cancellationToken) => next(request, cancellationToken);
    }

    public sealed class Step3<TRequest, TResponse> : global::Mediator.IPipelineBehavior<TRequest, TResponse>
        where TRequest : global::Mediator.IMessage
    {
        public ValueTask<TResponse> Handle(
            TRequest request,
            global::Mediator.MessageHandlerDelegate<TRequest, TResponse> next,
            CancellationToken cancellationToken) => next(request, cancellationToken);
    }

    public sealed class Step4<TRequest, TResponse> : global::Mediator.IPipelineBehavior<TRequest, TResponse>
        where TRequest : global::Mediator.IMessage
    {
        public ValueTask<TResponse> Handle(
            TRequest request,
            global::Mediator.MessageHandlerDelegate<TRequest, TResponse> next,
            CancellationToken cancellationToken) => next(request, cancellationToken);
    }
}
