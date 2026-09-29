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
    public sealed class Left : IRequest<string>
    {
    }

    public sealed class LeftHandler : IRequestHandler<Left, string>
    {
        public Task<string> Handle(Left request, CancellationToken cancellationToken) =>
            Task.FromResult("left");
    }

    public sealed class Right : IRequest<string>
    {
    }

    public sealed class RightHandler : IRequestHandler<Right, string>
    {
        public Task<string> Handle(Right request, CancellationToken cancellationToken) =>
            Task.FromResult("right");
    }

    public sealed class Tally
    {
        public List<string> Lines { get; } = new List<string>();
    }

    /// <summary>Records its own construction, so the tally counts how many were built.</summary>
    public sealed class Counted<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Counted(Tally tally) => tally.Lines.Add("built");

        public Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }

    public sealed class Marks<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly Tally ledger;

        public Marks(Tally ledger) => this.ledger = ledger;

        public Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            ledger.Lines.Add("first " + typeof(TRequest).Name);
            return next(cancellationToken);
        }
    }

    public sealed class MarksAgain<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly Tally ledger;

        public MarksAgain(Tally ledger) => this.ledger = ledger;

        public Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            ledger.Lines.Add("second " + typeof(TRequest).Name);
            return next(cancellationToken);
        }
    }

    public sealed class NoteEvery<TRequest> : IRequestPreProcessor<TRequest>
        where TRequest : notnull
    {
        private readonly Tally ledger;

        public NoteEvery(Tally ledger) => this.ledger = ledger;

        public Task Process(TRequest request, CancellationToken cancellationToken)
        {
            ledger.Lines.Add("before " + typeof(TRequest).Name);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// The overloads a migration needs: every one of these compiles over there, and none of them
    /// compiled here until now.
    /// </summary>
    public sealed class ConfigurationTests
    {
        private static ServiceProvider Build(Action<MediarionServiceConfiguration> configure)
        {
            var services = new ServiceCollection();
            services.AddSingleton<Tally>();
            services.AddMediarion(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<ConfigurationTests>();
                configure(configuration);
            });

            return services.BuildServiceProvider();
        }

        /// <remarks>
        /// Not that the property was stored — that the container obeyed it. A singleton behaviour
        /// is built once however many requests go through it, and the point of the overload is
        /// that a behaviour holding something expensive can say so.
        /// </remarks>
        [Fact]
        public async Task A_behaviour_is_built_once_when_it_is_a_singleton()
        {
            using ServiceProvider provider = Build(configuration =>
                configuration.AddOpenBehavior(typeof(Counted<,>), ServiceLifetime.Singleton));

            ISender sender = provider.GetRequiredService<ISender>();
            await sender.Send(new Left());
            await sender.Send(new Left());
            await sender.Send(new Left());

            provider.GetRequiredService<Tally>().Lines.ShouldBe(new[] { "built" });
        }

        [Fact]
        public async Task A_behaviour_is_built_every_time_by_default()
        {
            using ServiceProvider provider = Build(configuration =>
                configuration.AddOpenBehavior(typeof(Counted<,>)));

            ISender sender = provider.GetRequiredService<ISender>();
            await sender.Send(new Left());
            await sender.Send(new Left());

            provider.GetRequiredService<Tally>().Lines.ShouldBe(new[] { "built", "built" });
        }

        /// <remarks>
        /// Naming the contract is the whole point: the same open generic behaviour would wrap
        /// every request, and this one has to wrap exactly one.
        /// </remarks>
        [Fact]
        public async Task A_behaviour_can_be_pointed_at_one_request()
        {
            using ServiceProvider provider = Build(configuration =>
                configuration.AddBehavior(
                    typeof(IPipelineBehavior<Left, string>),
                    typeof(Marks<Left, string>)));

            ISender sender = provider.GetRequiredService<ISender>();
            await sender.Send(new Left());
            await sender.Send(new Right());

            provider.GetRequiredService<Tally>().Lines.ShouldBe(new[] { "first Left" });
        }

        [Fact]
        public async Task Several_open_behaviours_go_on_in_the_order_given()
        {
            using ServiceProvider provider = Build(configuration =>
                configuration.AddOpenBehaviors(new[] { typeof(Marks<,>), typeof(MarksAgain<,>) }));

            await provider.GetRequiredService<ISender>().Send(new Left());

            provider.GetRequiredService<Tally>().Lines
                .ShouldBe(new[] { "first Left", "second Left" });
        }

        /// <remarks>
        /// An open generic processor has to be registered against the open contract, which is a
        /// different path from a closed one: the container closes it per request rather than
        /// being told which requests it serves.
        /// </remarks>
        [Fact]
        public async Task An_open_generic_pre_processor_runs_for_every_request()
        {
            using ServiceProvider provider = Build(configuration =>
                configuration.AddOpenRequestPreProcessor(typeof(NoteEvery<>)));

            ISender sender = provider.GetRequiredService<ISender>();
            await sender.Send(new Left());
            await sender.Send(new Right());

            provider.GetRequiredService<Tally>().Lines
                .ShouldBe(new[] { "before Left", "before Right" });
        }

        [Fact]
        public void An_open_generic_processor_whose_parameters_do_not_line_up_says_so()
        {
            MediarionException error = Should.Throw<MediarionException>(() => Build(configuration =>
                configuration.AddOpenRequestPreProcessor(typeof(Counted<,>))));

            error.Message.ShouldContain("Counted");
            error.Message.ShouldContain("IRequestPreProcessor");
        }
    }
}
