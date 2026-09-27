using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mediarion.NotificationPublishers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Mediarion.Tests
{
    public sealed class Arrived : INotification
    {
        public string Who { get; set; } = string.Empty;
    }

    public sealed class Log
    {
        public List<string> Lines { get; } = new List<string>();
    }

    public sealed class Greet : INotificationHandler<Arrived>
    {
        private readonly Log log;

        public Greet(Log log)
        {
            this.log = log;
        }

        public Task Handle(Arrived notification, CancellationToken cancellationToken)
        {
            log.Lines.Add("greeted " + notification.Who);
            return Task.CompletedTask;
        }
    }

    public sealed class Announce : INotificationHandler<Arrived>
    {
        private readonly Log log;

        public Announce(Log log)
        {
            this.log = log;
        }

        public Task Handle(Arrived notification, CancellationToken cancellationToken)
        {
            log.Lines.Add("announced " + notification.Who);
            return Task.CompletedTask;
        }
    }

    public sealed class Unnoticed : INotification
    {
    }

    public sealed class PublishTests
    {
        private static ServiceProvider Build(INotificationPublisher? publisher = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<Log>();

            services.AddMediarion(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<PublishTests>();
                configuration.NotificationPublisher = publisher;
            });

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Every_handler_of_a_notification_sees_it()
        {
            ServiceProvider provider = Build();

            await provider.GetRequiredService<IPublisher>().Publish(new Arrived { Who = "Ada" });

            provider.GetRequiredService<Log>().Lines.ShouldBe(
                new[] { "announced Ada", "greeted Ada" },
                ignoreOrder: true);
        }

        [Fact]
        public async Task A_notification_nobody_handles_is_not_an_error()
        {
            IPublisher publisher = Build().GetRequiredService<IPublisher>();

            await publisher.Publish(new Unnoticed());
        }

        [Fact]
        public async Task Handlers_can_be_run_together_instead()
        {
            ServiceProvider provider = Build(new TaskWhenAllPublisher());

            await provider.GetRequiredService<IPublisher>().Publish(new Arrived { Who = "Ada" });

            provider.GetRequiredService<Log>().Lines.Count.ShouldBe(2);
        }

        [Fact]
        public async Task Something_that_is_not_a_notification_says_so()
        {
            IPublisher publisher = Build().GetRequiredService<IPublisher>();

            await Should.ThrowAsync<MediarionException>(() => publisher.Publish(new object()));
        }
    }
}
