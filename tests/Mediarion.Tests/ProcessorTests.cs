using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mediarion.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Mediarion.Tests
{
    public sealed class Measure : IRequest<int>
    {
        public string What { get; set; } = string.Empty;
    }

    public sealed class MeasureHandler : IRequestHandler<Measure, int>
    {
        private readonly Steps steps;

        public MeasureHandler(Steps steps)
        {
            this.steps = steps;
        }

        public Task<int> Handle(Measure request, CancellationToken cancellationToken)
        {
            steps.Add("handled");
            return Task.FromResult(request.What.Length);
        }
    }

    public sealed class Steps
    {
        public List<string> Taken { get; } = new List<string>();

        public void Add(string step) => Taken.Add(step);
    }

    public sealed class Before : IRequestPreProcessor<Measure>
    {
        private readonly Steps steps;

        public Before(Steps steps)
        {
            this.steps = steps;
        }

        public Task Process(Measure request, CancellationToken cancellationToken)
        {
            steps.Add("before");
            return Task.CompletedTask;
        }
    }

    public sealed class After : IRequestPostProcessor<Measure, int>
    {
        private readonly Steps steps;

        public After(Steps steps)
        {
            this.steps = steps;
        }

        public Task Process(Measure request, int response, CancellationToken cancellationToken)
        {
            steps.Add("after " + response.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Task.CompletedTask;
        }
    }

    public sealed class Around : IPipelineBehavior<Measure, int>
    {
        private readonly Steps steps;

        public Around(Steps steps)
        {
            this.steps = steps;
        }

        public async Task<int> Handle(
            Measure request,
            RequestHandlerDelegate<int> next,
            CancellationToken cancellationToken)
        {
            steps.Add("in");
            int response = await next(cancellationToken).ConfigureAwait(false);
            steps.Add("out");

            return response;
        }
    }

    public sealed class ProcessorTests
    {
        private static ServiceProvider Build(Action<MediarionServiceConfiguration> configure)
        {
            var services = new ServiceCollection();
            services.AddSingleton<Steps>();
            services.AddMediarion(configure);

            return services.BuildServiceProvider();
        }

        /// <remarks>
        /// A pre-processor runs before anything the application added, and a post-processor sees
        /// what the handler returned rather than what a behaviour did to it, so both sit outside
        /// the behaviours.
        /// </remarks>
        [Fact]
        public async Task Processors_run_outside_the_behaviours()
        {
            ServiceProvider provider = Build(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<ProcessorTests>();
                configuration.AddRequestPreProcessor<Before>();
                configuration.AddRequestPostProcessor<After>();
                configuration.AddBehavior<Around>();
            });

            await provider.GetRequiredService<ISender>().Send(new Measure { What = "four" });

            provider.GetRequiredService<Steps>().Taken
                .ShouldBe(new[] { "before", "in", "handled", "out", "after 4" });
        }

        /// <remarks>
        /// Off by default, the way the library this one is a drop-in for has it, so a codebase
        /// moved over does not start running processors that were sitting in the assembly.
        /// </remarks>
        [Fact]
        public async Task Processors_are_not_picked_up_by_a_scan_unless_asked_for()
        {
            ServiceProvider provider = Build(configuration =>
                configuration.RegisterServicesFromAssemblyContaining<ProcessorTests>());

            await provider.GetRequiredService<ISender>().Send(new Measure { What = "four" });

            provider.GetRequiredService<Steps>().Taken.ShouldBe(new[] { "handled" });
        }

        [Fact]
        public async Task A_scan_picks_them_up_when_it_is_asked_to()
        {
            ServiceProvider provider = Build(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<ProcessorTests>();
                configuration.AutoRegisterRequestProcessors = true;
            });

            await provider.GetRequiredService<ISender>().Send(new Measure { What = "four" });

            provider.GetRequiredService<Steps>().Taken
                .ShouldBe(new[] { "before", "handled", "after 4" });
        }

        /// <summary>
        /// A processor put into the container behind the library's back is not run.
        /// </summary>
        /// <remarks>
        /// The two behaviours that run the processors are only registered when the
        /// configuration knows there are some. They used to go on always — one empty loop each
        /// when there was nothing, which sounded free and cost 110 nanoseconds and 480 bytes on
        /// every request, because an open-generic registration is closed by the container per
        /// pair and each of them asks for an enumerable of its own.
        /// <para>
        /// This is the contract the other library has, and the one the generated registration
        /// has always had. Ask for the processor through the configuration and it runs.
        /// </para>
        /// </remarks>
        [Fact]
        public async Task A_processor_registered_behind_the_configuration_does_not_run()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Steps>();
            services.AddMediarion(configuration =>
                configuration.RegisterServicesFromAssemblyContaining<ProcessorTests>());

            services.AddTransient<IRequestPreProcessor<Measure>, Before>();

            using ServiceProvider provider = services.BuildServiceProvider();

            await provider.GetRequiredService<ISender>().Send(new Measure { What = "four" });

            provider.GetRequiredService<Steps>().Taken.ShouldBe(new[] { "handled" });
        }

        [Fact]
        public void Something_that_is_not_a_processor_says_so()
        {
            Should.Throw<MediarionException>(() => Build(configuration =>
                configuration.AddRequestPreProcessor<Steps>()));
        }
    }
}
