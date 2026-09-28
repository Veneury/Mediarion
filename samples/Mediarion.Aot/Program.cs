using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mediarion;
using Mediarion.Pipeline;
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

    public sealed class Fail : IRequest<string>
    {
    }

    public sealed class FailHandler : IRequestHandler<Fail, string>
    {
        public Task<string> Handle(Fail request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("nope");
    }

    public sealed class Rescue : IRequestExceptionHandler<Fail, string, InvalidOperationException>
    {
        public Task Handle(
            Fail request,
            InvalidOperationException exception,
            RequestExceptionHandlerState<string> state,
            CancellationToken cancellationToken)
        {
            state.SetHandled("rescued " + exception.Message);
            return Task.CompletedTask;
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
            // AddAppMediator is written by the generator from the handlers above: no assembly
            // is walked and no generic is closed at run time, which is the whole point.
            //
            // The behaviour is registered by hand and closed, because which behaviours apply and
            // in what order is this application's decision, not something to guess from what is
            // lying in the project.
            var services = new ServiceCollection();
            services.AddSingleton<Log>();
            services.AddAppMediator();
            services.AddTransient<IPipelineBehavior<Ping, string>, Shouting>();

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

            // The one that would need reflection if it were done the obvious way: choosing a
            // handler by the type of the exception that was thrown.
            Check(failures, "an exception handler", await mediator.Send(new Fail()), "rescued nope");

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
