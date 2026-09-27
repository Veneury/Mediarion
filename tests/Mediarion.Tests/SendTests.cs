using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Mediarion.Tests
{
    public sealed class Ping : IRequest<string>
    {
        public string Message { get; set; } = string.Empty;
    }

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> Handle(Ping request, CancellationToken cancellationToken) =>
            Task.FromResult("pong " + request.Message);
    }

    public sealed class Record : IRequest
    {
        public string What { get; set; } = string.Empty;
    }

    public sealed class Ledger
    {
        public List<string> Entries { get; } = new List<string>();
    }

    public sealed class RecordHandler : IRequestHandler<Record>
    {
        private readonly Ledger ledger;

        public RecordHandler(Ledger ledger)
        {
            this.ledger = ledger;
        }

        public Task Handle(Record request, CancellationToken cancellationToken)
        {
            ledger.Entries.Add(request.What);
            return Task.CompletedTask;
        }
    }

    public sealed class Unheard : IRequest<string>
    {
    }

    public sealed class Square : IPipelineBehavior<Ping, string>
    {
        public async Task<string> Handle(
            Ping request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            string response = await next(cancellationToken).ConfigureAwait(false);
            return "[" + response + "]";
        }
    }

    public sealed class Round : IPipelineBehavior<Ping, string>
    {
        public async Task<string> Handle(
            Ping request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            string response = await next(cancellationToken).ConfigureAwait(false);
            return "(" + response + ")";
        }
    }

    public sealed class SendTests
    {
        private static ServiceProvider Build(Action<MediarionServiceConfiguration>? extra = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<Ledger>();

            services.AddMediarion(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<SendTests>();
                extra?.Invoke(configuration);
            });

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task A_request_reaches_its_handler()
        {
            ISender sender = Build().GetRequiredService<ISender>();

            string response = await sender.Send(new Ping { Message = "there" });

            response.ShouldBe("pong there");
        }

        /// <remarks>
        /// A request with nothing to give back still goes through the pipeline, which is generic
        /// over a response, so it answers with <see cref="Unit"/>.
        /// </remarks>
        [Fact]
        public async Task A_request_with_no_response_reaches_its_handler()
        {
            ServiceProvider provider = Build();

            await provider.GetRequiredService<ISender>().Send(new Record { What = "kept" });

            provider.GetRequiredService<Ledger>().Entries.ShouldBe(new[] { "kept" });
        }

        [Fact]
        public async Task A_request_whose_type_is_only_known_at_run_time_still_reaches_it()
        {
            ISender sender = Build().GetRequiredService<ISender>();

            object? response = await sender.Send((object)new Ping { Message = "there" });

            response.ShouldBe("pong there");
        }

        [Fact]
        public async Task A_request_with_nothing_to_give_back_answers_with_nothing()
        {
            ServiceProvider provider = Build();

            object? response = await provider.GetRequiredService<ISender>().Send((object)new Record());

            response.ShouldBeNull();
        }

        [Fact]
        public async Task A_request_with_no_handler_says_so()
        {
            ISender sender = Build().GetRequiredService<ISender>();

            MediarionException error = await Should.ThrowAsync<MediarionException>(
                () => sender.Send(new Unheard()));

            error.Message.ShouldContain("Unheard");
        }

        /// <remarks>
        /// Registration order is pipeline order, outermost first. The two behaviours each wrap
        /// the response in a different bracket, so the answer says which ran outside the other
        /// rather than only saying that both ran.
        /// </remarks>
        [Fact]
        public async Task Behaviours_run_outermost_first()
        {
            ISender sender = Build(configuration =>
            {
                configuration.AddBehavior<Square>();
                configuration.AddBehavior<Round>();
            }).GetRequiredService<ISender>();

            string response = await sender.Send(new Ping { Message = "there" });

            response.ShouldBe("[(pong there)]");
        }

        [Fact]
        public async Task A_behaviour_that_does_not_call_on_stops_the_handler()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Ledger>();
            services.AddMediarion(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<SendTests>();
                configuration.AddBehavior<ShortCircuit>();
            });

            ServiceProvider provider = services.BuildServiceProvider();

            await provider.GetRequiredService<ISender>().Send(new Record { What = "kept" });

            provider.GetRequiredService<Ledger>().Entries.ShouldBeEmpty();
        }

        private sealed class ShortCircuit : IPipelineBehavior<Record, Unit>
        {
            public Task<Unit> Handle(
                Record request,
                RequestHandlerDelegate<Unit> next,
                CancellationToken cancellationToken) => Unit.Task;
        }
    }
}
