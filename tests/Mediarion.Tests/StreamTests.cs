using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Mediarion.Tests
{
    public sealed class CountTo : IStreamRequest<int>
    {
        public int Last { get; set; }
    }

    public sealed class CountToHandler : IStreamRequestHandler<CountTo, int>
    {
        private readonly Marks marks;

        public CountToHandler(Marks marks)
        {
            this.marks = marks;
        }

        public async IAsyncEnumerable<int> Handle(
            CountTo request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 1; i <= request.Last; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                marks.Produced.Add(i);
                await Task.Yield();

                yield return i;
            }
        }
    }

    /// <summary>What the handler produced, so a test can tell it stopped early.</summary>
    public sealed class Marks
    {
        public List<int> Produced { get; } = new List<int>();
    }

    public sealed class Doubling : IStreamPipelineBehavior<CountTo, int>
    {
        public async IAsyncEnumerable<int> Handle(
            CountTo request,
            StreamHandlerDelegate<int> next,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (int value in next().ConfigureAwait(false))
            {
                yield return value * 2;
            }
        }
    }

    public sealed class Unstreamed : IStreamRequest<int>
    {
    }

    public sealed class StreamTests
    {
        private static ServiceProvider Build(bool doubling = false)
        {
            var services = new ServiceCollection();
            services.AddSingleton<Marks>();
            services.AddSingleton<Log>();
            services.AddMediarion(configuration =>
                configuration.RegisterServicesFromAssemblyContaining<StreamTests>());
            services.AddMediarionStreaming(typeof(StreamTests).Assembly);

            if (doubling)
            {
                services.AddStreamBehavior(typeof(Doubling));
            }

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task A_stream_gives_its_values()
        {
            ISender sender = Build().GetRequiredService<ISender>();

            var seen = new List<int>();

            await foreach (int value in sender.CreateStream(new CountTo { Last = 3 }))
            {
                seen.Add(value);
            }

            seen.ShouldBe(new[] { 1, 2, 3 });
        }

        /// <remarks>
        /// The whole point of a stream: the consumer sees a value before the producer has
        /// finished. If this passed with the handler producing everything first, the values
        /// would all be there before the first iteration ran.
        /// </remarks>
        [Fact]
        public async Task The_consumer_sees_a_value_before_the_producer_has_finished()
        {
            ServiceProvider provider = Build();
            Marks marks = provider.GetRequiredService<Marks>();

            await foreach (int value in provider.GetRequiredService<ISender>()
                .CreateStream(new CountTo { Last = 100 }))
            {
                if (value == 1)
                {
                    marks.Produced.Count.ShouldBe(1);
                    break;
                }
            }
        }

        /// <remarks>
        /// Stopping the loop stops the handler. Without this a stream over a database would keep
        /// reading after the caller walked away.
        /// </remarks>
        [Fact]
        public async Task Breaking_out_stops_the_handler()
        {
            ServiceProvider provider = Build();

            await foreach (int value in provider.GetRequiredService<ISender>()
                .CreateStream(new CountTo { Last = 1000 }))
            {
                if (value == 3)
                {
                    break;
                }
            }

            provider.GetRequiredService<Marks>().Produced.Count.ShouldBe(3);
        }

        [Fact]
        public async Task Cancelling_stops_the_handler()
        {
            ServiceProvider provider = Build();
            using var cancellation = new CancellationTokenSource();

            await Should.ThrowAsync<OperationCanceledException>(async () =>
            {
                await foreach (int value in provider.GetRequiredService<ISender>()
                    .CreateStream(new CountTo { Last = 1000 }, cancellation.Token))
                {
                    if (value == 2)
                    {
                        cancellation.Cancel();
                    }
                }
            });

            provider.GetRequiredService<Marks>().Produced.Count.ShouldBeLessThan(10);
        }

        [Fact]
        public async Task A_behaviour_sees_every_value_on_its_way_out()
        {
            ISender sender = Build(doubling: true).GetRequiredService<ISender>();

            var seen = new List<int>();

            await foreach (int value in sender.CreateStream(new CountTo { Last = 3 }))
            {
                seen.Add(value);
            }

            seen.ShouldBe(new[] { 2, 4, 6 });
        }

        [Fact]
        public async Task A_stream_whose_type_is_only_known_at_run_time_still_works()
        {
            ISender sender = Build().GetRequiredService<ISender>();

            var seen = new List<object?>();

            await foreach (object? value in sender.CreateStream((object)new CountTo { Last = 2 }))
            {
                seen.Add(value);
            }

            seen.ShouldBe(new object?[] { 1, 2 });
        }

        [Fact]
        public async Task A_stream_with_no_handler_says_so()
        {
            ISender sender = Build().GetRequiredService<ISender>();

            MediarionException error = await Should.ThrowAsync<MediarionException>(async () =>
            {
                await foreach (int value in sender.CreateStream(new Unstreamed()))
                {
                    _ = value;
                }
            });

            error.Message.ShouldContain("Unstreamed");
        }

        /// <remarks>
        /// A sender that cannot stream says so plainly rather than failing somewhere confusing.
        /// </remarks>
        [Fact]
        public void A_sender_without_the_streaming_package_registered_says_so()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Marks>();
            services.AddMediarion(configuration =>
                configuration.RegisterServicesFromAssemblyContaining<StreamTests>());

            using ServiceProvider provider = services.BuildServiceProvider();

            MediarionException error = Should.Throw<MediarionException>(
                () => provider.GetRequiredService<ISender>().CreateStream(new CountTo { Last = 1 }));

            error.Message.ShouldContain("AddMediarionStreaming");
        }

        /// <remarks>
        /// Streaming replaces the mediator, so the half that was already there has to keep
        /// working.
        /// </remarks>
        [Fact]
        public async Task Sending_and_publishing_still_work_afterwards()
        {
            ServiceProvider provider = Build();

            (await provider.GetRequiredService<ISender>().Send(new Ping { Message = "there" }))
                .ShouldBe("pong there");

            await provider.GetRequiredService<IPublisher>().Publish(new Arrived { Who = "Ada" });
        }

        /// <remarks>
        /// A stream behaviour added where every other behaviour is added. The configuration is in
        /// the package with no streaming in it, so it only records the type; AddMediarionStreaming
        /// is what registers it, and this is the test that the hand-off works.
        /// </remarks>
        [Fact]
        public async Task A_stream_behaviour_can_be_added_to_the_configuration()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Marks>();
            services.AddSingleton<Log>();
            services.AddMediarion(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<StreamTests>();
                configuration.AddStreamBehavior<Doubling>();
            });
            services.AddMediarionStreaming(typeof(StreamTests).Assembly);

            using ServiceProvider provider = services.BuildServiceProvider();
            ISender sender = provider.GetRequiredService<ISender>();

            var seen = new List<int>();

            await foreach (int value in sender.CreateStream(new CountTo { Last = 3 }))
            {
                seen.Add(value);
            }

            seen.ShouldBe(new[] { 2, 4, 6 });
        }
    }
}
