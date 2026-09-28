using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Mediarion.Benchmarks.ForGenerated;
using Microsoft.Extensions.DependencyInjection;

namespace Mediarion.Benchmarks
{
    /// <summary>
    /// One request through a handler, five ways.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The handler does nothing on purpose. What is being measured is what the library adds
    /// around it: finding the handler, building the pipeline, and the calls to get there. A
    /// handler that did real work would bury exactly the thing under test.
    /// </para>
    /// <para>
    /// Every entrant is measured in the same run against the same hand-written baseline, which
    /// is a direct call to the handler through its interface. The multiple is what travels
    /// between machines; the nanoseconds are not.
    /// </para>
    /// </remarks>
    [MemoryDiagnoser]
    public class SendBenchmarks
    {
        private ByHand.PingHandler handwritten = null!;
        private global::Mediarion.ISender mediarion = null!;
        private ForGenerated.AppMediator generated = null!;
        private global::MediatR.ISender mediatr = null!;
        private global::Mediator.IMediator mediator = null!;

        private ByHand.Ping handwrittenRequest = null!;
        private ForMediarion.Ping mediarionRequest = null!;
        private ForMediatR.Ping mediatrRequest = null!;
        private ForMediator.Ping mediatorRequest = null!;

        [GlobalSetup]
        public void Setup()
        {
            handwritten = new ByHand.PingHandler();
            handwrittenRequest = new ByHand.Ping { Message = "there" };

            var forMediarion = new ServiceCollection();
            forMediarion.AddMediarion(cfg => cfg.RegisterServicesFromAssemblyContaining<SendBenchmarks>());
            ServiceProvider mediarionProvider = forMediarion.BuildServiceProvider();
            mediarion = mediarionProvider.GetRequiredService<global::Mediarion.ISender>();
            mediarionRequest = new ForMediarion.Ping { Message = "there" };

            var forGenerated = new ServiceCollection();
            forGenerated.AddAppMediator();
            generated = new ForGenerated.AppMediator(forGenerated.BuildServiceProvider());

            var forMediatR = new ServiceCollection();
            forMediatR.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SendBenchmarks).Assembly));
            mediatr = forMediatR.BuildServiceProvider().GetRequiredService<global::MediatR.ISender>();
            mediatrRequest = new ForMediatR.Ping { Message = "there" };

            var forMediator = new ServiceCollection();
            forMediator.AddMediator();
            mediator = forMediator.BuildServiceProvider().GetRequiredService<global::Mediator.IMediator>();
            mediatorRequest = new ForMediator.Ping { Message = "there" };
        }

        [Benchmark(Baseline = true)]
        public Task<string> Manual() =>
            handwritten.Handle(handwrittenRequest, CancellationToken.None);

        [Benchmark]
        public Task<string> Mediarion_Runtime() =>
            mediarion.Send(mediarionRequest);

        [Benchmark]
        public Task<string> Mediarion_Generated() =>
            generated.Send(mediarionRequest);

        [Benchmark]
        public Task<string> MediatR() =>
            mediatr.Send(mediatrRequest);

        [Benchmark]
        public ValueTask<string> Mediator() =>
            mediator.Send(mediatorRequest);
    }
}
