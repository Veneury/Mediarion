using System.Threading;
using System.Threading.Tasks;

// One request and one handler per library. Each lives in a namespace deliberately not named
// after its library: a namespace in the enclosing chain beats a using directive, so calling one
// of these Mediarion would make every unqualified Mediarion.X in this project resolve here
// instead of to the library. That is not a hypothetical — it is how the migration sample was
// wrong the first time it was written, and this file was too.
namespace Mediarion.Benchmarks.ByHand
{
    public sealed class Ping
    {
        public string Message { get; set; } = string.Empty;
    }

    public interface IPingHandler
    {
        Task<string> Handle(Ping request, CancellationToken cancellationToken);
    }

    /// <remarks>
    /// The baseline. Called through its interface rather than as a concrete type, so that what
    /// the libraries are compared against is a handler reached the way an application would
    /// reach one, not a call the JIT can inline away entirely.
    /// </remarks>
    public sealed class PingHandler : IPingHandler
    {
        public Task<string> Handle(Ping request, CancellationToken cancellationToken) =>
            Task.FromResult("pong " + request.Message);
    }
}

namespace Mediarion.Benchmarks.ForMediarion
{
    public sealed class Ping : global::Mediarion.IRequest<string>
    {
        public string Message { get; set; } = string.Empty;
    }

    public sealed class PingHandler : global::Mediarion.IRequestHandler<Ping, string>
    {
        public Task<string> Handle(Ping request, CancellationToken cancellationToken) =>
            Task.FromResult("pong " + request.Message);
    }
}

namespace Mediarion.Benchmarks.ForGenerated
{
    /// <summary>The same handlers again, reached through a dispatch written while compiling.</summary>
    [global::Mediarion.GeneratedMediator]
    public sealed partial class AppMediator
    {
    }
}

namespace Mediarion.Benchmarks.ForMediatR
{
    public sealed class Ping : global::MediatR.IRequest<string>
    {
        public string Message { get; set; } = string.Empty;
    }

    public sealed class PingHandler : global::MediatR.IRequestHandler<Ping, string>
    {
        public Task<string> Handle(Ping request, CancellationToken cancellationToken) =>
            Task.FromResult("pong " + request.Message);
    }
}

namespace Mediarion.Benchmarks.ForMediator
{
    public sealed class Ping : global::Mediator.IRequest<string>
    {
        public string Message { get; set; } = string.Empty;
    }

    public sealed class PingHandler : global::Mediator.IRequestHandler<Ping, string>
    {
        public ValueTask<string> Handle(Ping request, CancellationToken cancellationToken) =>
            new ValueTask<string>("pong " + request.Message);
    }
}
