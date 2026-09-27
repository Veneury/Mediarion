using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mediarion;
using Microsoft.Extensions.DependencyInjection;

namespace AotSample
{
    public sealed class Ping : IRequest<string>
    {
        public string Message { get; set; } = string.Empty;
    }

    public sealed class Note : IRequest
    {
        public string What { get; set; } = string.Empty;
    }

    public sealed class Rang : INotification
    {
        public string Message { get; set; } = string.Empty;
    }

    public sealed class Log
    {
        public List<string> Lines { get; } = new List<string>();
    }

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> Handle(Ping request, CancellationToken cancellationToken) =>
            Task.FromResult("pong " + request.Message);
    }

    public sealed class NoteHandler : IRequestHandler<Note>
    {
        private readonly Log log;

        public NoteHandler(Log log)
        {
            this.log = log;
        }

        public Task Handle(Note request, CancellationToken cancellationToken)
        {
            log.Lines.Add("noted " + request.What);
            return Task.CompletedTask;
        }
    }

    public sealed class Answer : INotificationHandler<Rang>
    {
        private readonly Log log;

        public Answer(Log log)
        {
            this.log = log;
        }

        public Task Handle(Rang notification, CancellationToken cancellationToken)
        {
            log.Lines.Add("answered " + notification.Message);
            return Task.CompletedTask;
        }
    }

    public sealed class Shouting : IPipelineBehavior<Ping, string>
    {
        public async Task<string> Handle(
            Ping request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            string response = await next(cancellationToken).ConfigureAwait(false);
            return response.ToUpperInvariant();
        }
    }

    /// <summary>
    /// The class the generator implements. There is nothing in it: the dispatch is written from
    /// the handlers above, while the project compiles.
    /// </summary>
    [GeneratedMediator]
    public sealed partial class AppMediator
    {
    }

    /// <summary>
    /// Published ahead of time and run by CI. It checks its own answers and exits non-zero when
    /// one is wrong, so "it works under AOT" is something the build finds out rather than
    /// something the readme says.
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main()
        {
            // Registered by hand and not by scanning an assembly, because a scan is reflection
            // and reflection is the thing this sample exists to do without.
            var services = new ServiceCollection();
            services.AddSingleton<Log>();
            services.AddSingleton<INotificationPublisher, Mediarion.NotificationPublishers.ForeachAwaitPublisher>();
            services.AddTransient<IRequestHandler<Ping, string>, PingHandler>();
            services.AddTransient<IRequestHandler<Note>, NoteHandler>();
            services.AddTransient<IRequestHandler<Note, Unit>, VoidHandlerAdapter<Note>>();
            services.AddTransient<INotificationHandler<Rang>, Answer>();
            services.AddTransient<IPipelineBehavior<Ping, string>, Shouting>();
            services.AddSingleton<IMediator, AppMediator>();

            using ServiceProvider provider = services.BuildServiceProvider();

            IMediator mediator = provider.GetRequiredService<IMediator>();
            Log log = provider.GetRequiredService<Log>();

            var failures = new List<string>();

            Check(failures, "a request with a response", await mediator.Send(new Ping { Message = "there" }), "PONG THERE");

            await mediator.Send(new Note { What = "kept" });
            Check(failures, "a request with no response", string.Join(",", log.Lines), "noted kept");

            await mediator.Publish(new Rang { Message = "bell" });
            Check(failures, "a notification", string.Join(",", log.Lines), "noted kept,answered bell");

            object? boxed = await mediator.Send((object)new Ping { Message = "again" });
            Check(failures, "a request sent as object", boxed?.ToString(), "PONG AGAIN");

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            if (failures.Count > 0)
            {
                return 1;
            }

            Console.WriteLine("Mediarion ahead-of-time sample: every check passed.");
            return 0;
        }

        private static void Check(List<string> failures, string what, string? actual, string expected)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                failures.Add(what + ": expected '" + expected + "' and got '" + actual + "'");
            }
        }
    }
}
