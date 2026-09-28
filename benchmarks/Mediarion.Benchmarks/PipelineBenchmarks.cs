using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Mediarion.Benchmarks.ForGenerated;
using Microsoft.Extensions.DependencyInjection;

namespace Mediarion.Benchmarks
{
    /// <summary>
    /// One request through four behaviours and a handler. This is the scenario CI holds to a
    /// budget.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other scenario measures an empty pipeline, which is the common case and the least
    /// interesting one to guard: its floor is seventeen nanoseconds and the ratios move by a third
    /// between runs of untouched code, which is why nothing in CI is allowed to depend on them.
    /// Four behaviours is what an application that uses a mediator seriously tends to have —
    /// logging, validation, a transaction, a metric — and it costs two hundred nanoseconds, which
    /// is a floor a real regression shows up against.
    /// </para>
    /// <para>
    /// What the budget compares against is <see cref="MediatR"/> in the same run, and not
    /// <see cref="Manual"/>. The hand-written version is four private calls the JIT flattens into
    /// one, ending at a cached task, so it measures half a nanosecond and the multiple over it is
    /// four hundred and moves by half between runs of the same code. It is kept because it says
    /// what the pipeline costs in absolute terms, and it is useless as a divisor. The other
    /// library does the same work through a pipeline it builds the same way, on the same machine,
    /// in the same run — so it moves with the runner, which is the whole point of a ratio — and
    /// 12.5.0 is frozen under Apache-2.0, so the reference cannot shift underneath the budget.
    /// </para>
    /// </remarks>
    [MemoryDiagnoser]
    public class PipelineBenchmarks
    {
        private ByHand.Piped handwritten = null!;
        private global::Mediarion.ISender mediarion = null!;
        private ForGenerated.AppMediator generated = null!;
        private global::MediatR.ISender mediatr = null!;

        private ByHand.Work handwrittenRequest = null!;
        private ForMediarion.Work mediarionRequest = null!;
        private ForMediatR.Work mediatrRequest = null!;

        [GlobalSetup]
        public void Setup()
        {
            handwritten = new ByHand.Piped();
            handwrittenRequest = new ByHand.Work { Value = 1 };

            var forMediarion = new ServiceCollection();
            forMediarion.AddMediarion(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<PipelineBenchmarks>();
                configuration.AddOpenBehavior(typeof(ForMediarion.Step1<,>));
                configuration.AddOpenBehavior(typeof(ForMediarion.Step2<,>));
                configuration.AddOpenBehavior(typeof(ForMediarion.Step3<,>));
                configuration.AddOpenBehavior(typeof(ForMediarion.Step4<,>));
            });

            mediarion = forMediarion.BuildServiceProvider().GetRequiredService<global::Mediarion.ISender>();
            mediarionRequest = new ForMediarion.Work { Value = 1 };

            // The generated mediator gets the same four behaviours, registered closed, which is
            // what an application published ahead of time has to do anyway.
            var forGenerated = new ServiceCollection();
            forGenerated.AddAppMediator();
            forGenerated.AddTransient<
                global::Mediarion.IPipelineBehavior<ForMediarion.Work, int>,
                ForMediarion.Step1<ForMediarion.Work, int>>();
            forGenerated.AddTransient<
                global::Mediarion.IPipelineBehavior<ForMediarion.Work, int>,
                ForMediarion.Step2<ForMediarion.Work, int>>();
            forGenerated.AddTransient<
                global::Mediarion.IPipelineBehavior<ForMediarion.Work, int>,
                ForMediarion.Step3<ForMediarion.Work, int>>();
            forGenerated.AddTransient<
                global::Mediarion.IPipelineBehavior<ForMediarion.Work, int>,
                ForMediarion.Step4<ForMediarion.Work, int>>();

            generated = new ForGenerated.AppMediator(forGenerated.BuildServiceProvider());

            var forMediatR = new ServiceCollection();
            forMediatR.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssembly(typeof(PipelineBenchmarks).Assembly);
                configuration.AddOpenBehavior(typeof(ForMediatR.Step1<,>));
                configuration.AddOpenBehavior(typeof(ForMediatR.Step2<,>));
                configuration.AddOpenBehavior(typeof(ForMediatR.Step3<,>));
                configuration.AddOpenBehavior(typeof(ForMediatR.Step4<,>));
            });

            mediatr = forMediatR.BuildServiceProvider().GetRequiredService<global::MediatR.ISender>();
            mediatrRequest = new ForMediatR.Work { Value = 1 };
        }

        [Benchmark(Baseline = true)]
        public Task<int> MediatR() =>
            mediatr.Send(mediatrRequest);

        [Benchmark]
        public Task<int> Mediarion_Runtime() =>
            mediarion.Send(mediarionRequest);

        [Benchmark]
        public Task<int> Mediarion_Generated() =>
            generated.Send(mediarionRequest);

        [Benchmark]
        public Task<int> Manual() =>
            handwritten.Run(handwrittenRequest, CancellationToken.None);
    }
}
