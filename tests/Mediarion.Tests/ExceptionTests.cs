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
    public sealed class Divide : IRequest<int>
    {
        public int By { get; set; }
    }

    public sealed class DivideHandler : IRequestHandler<Divide, int>
    {
        public Task<int> Handle(Divide request, CancellationToken cancellationToken) =>
            request.By == 0
                ? throw new DivideByZeroException("by zero")
                : Task.FromResult(100 / request.By);
    }

    public sealed class Trail
    {
        public List<string> Lines { get; } = new List<string>();
    }

    public sealed class SubstituteZero : IRequestExceptionHandler<Divide, int, DivideByZeroException>
    {
        private readonly Trail trail;

        public SubstituteZero(Trail trail)
        {
            this.trail = trail;
        }

        public Task Handle(
            Divide request,
            DivideByZeroException exception,
            RequestExceptionHandlerState<int> state,
            CancellationToken cancellationToken)
        {
            trail.Lines.Add("handled " + exception.Message);
            state.SetHandled(-1);

            return Task.CompletedTask;
        }
    }

    /// <summary>Registered for an exception the handler never throws, so it stands aside.</summary>
    public sealed class NeverMatches : IRequestExceptionHandler<Divide, int, FormatException>
    {
        private readonly Trail trail;

        public NeverMatches(Trail trail)
        {
            this.trail = trail;
        }

        public Task Handle(
            Divide request,
            FormatException exception,
            RequestExceptionHandlerState<int> state,
            CancellationToken cancellationToken)
        {
            trail.Lines.Add("should not have run");
            state.SetHandled(-99);

            return Task.CompletedTask;
        }
    }

    public sealed class Explode : IRequest<int>
    {
    }

    public sealed class ExplodeHandler : IRequestHandler<Explode, int>
    {
        public Task<int> Handle(Explode request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }

    public sealed class NoteTheBang : IRequestExceptionAction<Explode, InvalidOperationException>
    {
        private readonly Trail trail;

        public NoteTheBang(Trail trail)
        {
            this.trail = trail;
        }

        public Task Execute(Explode request, InvalidOperationException exception, CancellationToken cancellationToken)
        {
            trail.Lines.Add("noted " + exception.Message);
            return Task.CompletedTask;
        }
    }

    public sealed class CatchAll : IRequest<int>
    {
    }

    public sealed class CatchAllHandler : IRequestHandler<CatchAll, int>
    {
        public Task<int> Handle(CatchAll request, CancellationToken cancellationToken) =>
            throw new FormatException("anything");
    }

    /// <summary>
    /// Written for <see cref="Exception"/> itself, which is the shape that needs no adapter.
    /// </summary>
    public sealed class CatchAnything : IRequestExceptionHandler<CatchAll, int>
    {
        private readonly Trail trail;

        public CatchAnything(Trail trail)
        {
            this.trail = trail;
        }

        public Task Handle(
            CatchAll request,
            Exception exception,
            RequestExceptionHandlerState<int> state,
            CancellationToken cancellationToken)
        {
            trail.Lines.Add("caught " + exception.GetType().Name);
            state.SetHandled(0);

            return Task.CompletedTask;
        }
    }

    public sealed class ExceptionTests
    {
        private static ServiceProvider Build()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Trail>();
            services.AddMediarion(configuration =>
                configuration.RegisterServicesFromAssemblyContaining<ExceptionTests>());

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task A_handler_can_answer_in_the_exceptions_place()
        {
            ServiceProvider provider = Build();

            int answer = await provider.GetRequiredService<ISender>().Send(new Divide { By = 0 });

            answer.ShouldBe(-1);
            provider.GetRequiredService<Trail>().Lines.ShouldBe(new[] { "handled by zero" });
        }

        [Fact]
        public async Task A_request_that_does_not_throw_is_left_alone()
        {
            ServiceProvider provider = Build();

            (await provider.GetRequiredService<ISender>().Send(new Divide { By = 4 })).ShouldBe(25);
            provider.GetRequiredService<Trail>().Lines.ShouldBeEmpty();
        }

        /// <remarks>
        /// Choosing by the exception's type is the one thing here that would need reflection, so
        /// it is done by a type test inside an adapter instead. This is that test: a handler
        /// registered for another exception type must stand aside rather than answer.
        /// </remarks>
        [Fact]
        public async Task A_handler_for_another_exception_stands_aside()
        {
            ServiceProvider provider = Build();

            int answer = await provider.GetRequiredService<ISender>().Send(new Divide { By = 0 });

            answer.ShouldBe(-1);
            provider.GetRequiredService<Trail>().Lines.ShouldNotContain("should not have run");
        }

        /// <remarks>
        /// An action is for the things that should happen on a failure without changing what the
        /// caller sees, so the exception carries on out.
        /// </remarks>
        [Fact]
        public async Task An_action_runs_and_the_exception_still_comes_out()
        {
            ServiceProvider provider = Build();

            InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(
                () => provider.GetRequiredService<ISender>().Send(new Explode()));

            error.Message.ShouldBe("boom");
            provider.GetRequiredService<Trail>().Lines.ShouldBe(new[] { "noted boom" });
        }

        /// <remarks>
        /// Nothing is wrapped: a catch written against the exception the handler threw still
        /// works, which is what makes the mediator something you can put in front of code that
        /// already has error handling.
        /// </remarks>
        /// <remarks>
        /// A handler written for Exception itself is registered as the shape the behaviour
        /// resolves, so it must not be wrapped in the adapter that makes the others look like
        /// that shape: the adapter asks for the same service type it is registered as, and would
        /// be handed itself.
        /// </remarks>
        [Fact]
        public async Task A_handler_for_any_exception_is_not_wrapped_around_itself()
        {
            ServiceProvider provider = Build();

            int answer = await provider.GetRequiredService<ISender>().Send(new CatchAll());

            answer.ShouldBe(0);
            provider.GetRequiredService<Trail>().Lines.ShouldBe(new[] { "caught FormatException" });
        }

        [Fact]
        public async Task An_unhandled_exception_comes_out_as_it_was_thrown()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Trail>();
            services.AddMediarion(configuration =>
                configuration.RegisterServicesFromAssemblyContaining<ExceptionTests>());

            using ServiceProvider provider = services.BuildServiceProvider();

            InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(
                () => provider.GetRequiredService<ISender>().Send(new Explode()));

            error.ShouldBeOfType<InvalidOperationException>();
            error.InnerException.ShouldBeNull();
        }
    }
}
