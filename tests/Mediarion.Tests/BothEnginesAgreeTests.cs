using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Mediarion.Tests
{
    /// <summary>
    /// The class the generator implements, over every handler in this test project.
    /// </summary>
    [GeneratedMediator]
    public sealed partial class GeneratedTestMediator
    {
    }

    /// <summary>
    /// The guarantee the generated engine rests on. One works out which handler answers a
    /// request while the program runs, the other had the same question answered while the
    /// project compiled. The only thing keeping them honest is sending the same request through
    /// both and comparing.
    /// </summary>
    public sealed class BothEnginesAgreeTests
    {
        private static ServiceProvider Build()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Ledger>();
            services.AddSingleton<Log>();
            services.AddSingleton<Steps>();

            services.AddMediarion(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<BothEnginesAgreeTests>();
                configuration.AddBehavior<Square>();
                configuration.AddBehavior<Round>();
            });

            return services.BuildServiceProvider();
        }

        private static GeneratedTestMediator Generated(ServiceProvider provider) =>
            new GeneratedTestMediator(provider, provider.GetRequiredService<INotificationPublisher>());

        [Fact]
        public async Task They_agree_on_a_request_with_a_response()
        {
            ServiceProvider provider = Build();

            string byRuntime = await provider.GetRequiredService<ISender>()
                .Send(new Ping { Message = "there" });

            string byGenerator = await Generated(provider).Send(new Ping { Message = "there" });

            byGenerator.ShouldBe(byRuntime);
            byGenerator.ShouldBe("[(pong there)]");
        }

        /// <remarks>
        /// The behaviours are the point here as much as the handler: a generated dispatch that
        /// skipped them would still return the right string from the handler.
        /// </remarks>
        [Fact]
        public async Task They_agree_on_a_request_with_no_response()
        {
            ServiceProvider provider = Build();

            await provider.GetRequiredService<ISender>().Send(new Record { What = "runtime" });
            await Generated(provider).Send(new Record { What = "generated" });

            provider.GetRequiredService<Ledger>().Entries.ShouldBe(new[] { "runtime", "generated" });
        }

        [Fact]
        public async Task They_agree_on_a_request_sent_as_an_object()
        {
            ServiceProvider provider = Build();

            object? byRuntime = await provider.GetRequiredService<ISender>()
                .Send((object)new Record { What = "one" });

            object? byGenerator = await Generated(provider).Send((object)new Record { What = "two" });

            byGenerator.ShouldBe(byRuntime);
            byGenerator.ShouldBe(Unit.Value);
        }

        [Fact]
        public async Task They_agree_on_a_notification()
        {
            ServiceProvider provider = Build();

            await provider.GetRequiredService<IPublisher>().Publish(new Arrived { Who = "Ada" });
            int afterRuntime = provider.GetRequiredService<Log>().Lines.Count;

            await Generated(provider).Publish(new Arrived { Who = "Grace" });

            provider.GetRequiredService<Log>().Lines.Count.ShouldBe(afterRuntime * 2);
        }

        [Fact]
        public async Task They_agree_that_a_notification_nobody_handles_is_not_an_error()
        {
            ServiceProvider provider = Build();

            await provider.GetRequiredService<IPublisher>().Publish(new Unnoticed());
            await Generated(provider).Publish(new Unnoticed());
        }

        [Fact]
        public async Task They_agree_that_a_request_with_no_handler_is_refused()
        {
            ServiceProvider provider = Build();

            await Should.ThrowAsync<MediarionException>(
                () => provider.GetRequiredService<ISender>().Send(new Unheard()));

            await Should.ThrowAsync<MediarionException>(
                () => Generated(provider).Send(new Unheard()));
        }

        [Fact]
        public async Task They_agree_that_something_which_is_not_a_notification_is_refused()
        {
            ServiceProvider provider = Build();

            await Should.ThrowAsync<MediarionException>(
                () => provider.GetRequiredService<IPublisher>().Publish(new object()));

            await Should.ThrowAsync<MediarionException>(
                () => Generated(provider).Publish(new object()));
        }
    }
}
