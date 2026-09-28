using Shouldly;
using Xunit;

namespace Mediarion.SourceGenerator.Tests
{
    public sealed class GeneratorTests
    {
        private const string Handled = @"
using System.Threading;
using System.Threading.Tasks;
using Mediarion;

public sealed class Ping : IRequest<string> { }

public sealed class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken) => Task.FromResult(""pong"");
}
";

        [Fact]
        public void A_marked_class_gets_a_dispatch()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Handled + @"
[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Report().ShouldBeEmpty();
            outcome.Generated.ShouldContain("case global::Ping typed:");
            outcome.Generated.ShouldContain("global::Mediarion.RequestPipeline.Run<global::Ping, string>");
        }

        /// <remarks>
        /// The point of the generated path. A reader can check the claim by reading the file.
        /// </remarks>
        [Fact]
        public void The_dispatch_reflects_over_nothing()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Handled + @"
[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Generated.ShouldNotContain("MakeGenericType");
            outcome.Generated.ShouldNotContain("Activator");
            outcome.Generated.ShouldNotContain("GetInterfaces");
            outcome.Generated.ShouldNotContain("Assembly");
        }

        [Fact]
        public void Nothing_is_generated_without_a_marked_class()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Handled);

            outcome.Generated.ShouldBeEmpty();
        }

        /// <remarks>
        /// An empty switch is not valid C#. A marked class in a project with no handlers of its
        /// own is an odd thing to write and it has to compile, which this did not until a
        /// benchmark project turned out to be exactly that shape.
        /// </remarks>
        [Fact]
        public void A_mediator_with_nothing_to_dispatch_still_compiles()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(@"
using Mediarion;

[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Report().ShouldBeEmpty();
            outcome.Generated.ShouldNotContain("switch (request)");
            outcome.Generated.ShouldNotContain("switch (notification)");
        }

        /// <remarks>
        /// The same again with requests but no notifications, which is the common half of it.
        /// </remarks>
        [Fact]
        public void A_mediator_with_no_notifications_still_compiles()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Handled + @"
[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Report().ShouldBeEmpty();
            outcome.Generated.ShouldContain("switch (request)");
            outcome.Generated.ShouldNotContain("switch (notification)");
        }

        [Fact]
        public void A_class_that_is_not_partial_is_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Handled + @"
[GeneratedMediator]
public sealed class AppMediator { }");

            outcome.Reported("MDR0001").ShouldBeTrue(outcome.Report());
        }

        [Fact]
        public void Two_handlers_for_one_request_are_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Handled + @"
public sealed class SecondPingHandler : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken) => Task.FromResult(""pong"");
}

[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Reported("MDR0002").ShouldBeTrue(outcome.Report());
        }

        /// <remarks>
        /// A warning and not an error, because the handler may be registered from an assembly
        /// the generator cannot see. What it catches is the ordinary case: somebody wrote the
        /// request and never wrote the handler.
        /// </remarks>
        [Fact]
        public void A_request_with_no_handler_is_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Handled + @"
public sealed class Unheard : IRequest<int> { }

[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Reported("MDR0003").ShouldBeTrue(outcome.Report());
        }

        [Fact]
        public void A_request_that_has_one_is_not_reported()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Handled + @"
[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Reported("MDR0003").ShouldBeFalse(outcome.Report());
        }

        /// <remarks>
        /// A request with nothing to give back reaches the pipeline as one that answers with
        /// <c>Unit</c>, and the registration has to add the adapter that makes it look that way.
        /// </remarks>
        [Fact]
        public void A_request_with_no_response_gets_its_adapter()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(@"
using System.Threading;
using System.Threading.Tasks;
using Mediarion;

public sealed class Note : IRequest { }

public sealed class NoteHandler : IRequestHandler<Note>
{
    public Task Handle(Note request, CancellationToken cancellationToken) => Task.CompletedTask;
}

[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Report().ShouldBeEmpty();
            outcome.Generated.ShouldContain("global::Mediarion.VoidHandlerAdapter<global::Note>");
            outcome.Generated.ShouldContain("global::Mediarion.RequestPipeline.Run<global::Note, global::Mediarion.Unit>");
        }

        [Fact]
        public void A_notification_gets_a_case_and_its_handler_a_registration()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(@"
using System.Threading;
using System.Threading.Tasks;
using Mediarion;

public sealed class Rang : INotification { }

public sealed class Answer : INotificationHandler<Rang>
{
    public Task Handle(Rang notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Report().ShouldBeEmpty();
            outcome.Generated.ShouldContain("global::Mediarion.NotificationPipeline.Run<global::Rang>");
            outcome.Generated.ShouldContain("global::Mediarion.INotificationHandler<global::Rang>, global::Answer");
        }

        /// <remarks>
        /// Off unless the attribute asks, which is what <c>AddMediarion</c> does with its own
        /// flag. The two ways of registering have to agree or the same application behaves
        /// differently depending on which one started it.
        /// </remarks>
        [Fact]
        public void Processors_are_left_out_of_the_registration_unless_asked_for()
        {
            const string Source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediarion;
using Mediarion.Pipeline;

public sealed class Ping : IRequest<string> { }

public sealed class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken) => Task.FromResult(""pong"");
}

public sealed class Before : IRequestPreProcessor<Ping>
{
    public Task Process(Ping request, CancellationToken cancellationToken) => Task.CompletedTask;
}
";

            GeneratorHarness.Run(Source + @"
[GeneratedMediator]
public sealed partial class AppMediator { }")
                .Generated.ShouldNotContain("RequestPreProcessorBehavior");

            GeneratorHarness.Run(Source + @"
[GeneratedMediator(RegisterRequestProcessors = true)]
public sealed partial class AppMediator { }")
                .Generated.ShouldContain("RequestPreProcessorBehavior");
        }

        /// <remarks>
        /// The adapter is what makes choosing a handler by the exception's type possible without
        /// closing a generic over a type learned at run time, so the registration has to name it.
        /// </remarks>
        [Fact]
        public void An_exception_handler_gets_its_adapter_and_its_behaviour()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Handled + @"
public sealed class Rescue
    : Mediarion.Pipeline.IRequestExceptionHandler<Ping, string, System.InvalidOperationException>
{
    public Task Handle(
        Ping request,
        System.InvalidOperationException exception,
        Mediarion.Pipeline.RequestExceptionHandlerState<string> state,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Report().ShouldBeEmpty();
            outcome.Generated.ShouldContain("global::Mediarion.Pipeline.ExceptionHandlerAdapter<global::Ping, string, global::System.InvalidOperationException>");
            outcome.Generated.ShouldContain("RequestExceptionProcessorBehavior<global::Ping, string>");
        }

        /// <remarks>
        /// One written for Exception itself is already the shape the behaviour resolves. Wrapping
        /// it would hand the adapter itself, and it would call itself until the stack ran out.
        /// </remarks>
        [Fact]
        public void A_handler_for_any_exception_gets_no_adapter()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Handled + @"
public sealed class CatchAnything
    : Mediarion.Pipeline.IRequestExceptionHandler<Ping, string>
{
    public Task Handle(
        Ping request,
        System.Exception exception,
        Mediarion.Pipeline.RequestExceptionHandlerState<string> state,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Report().ShouldBeEmpty();
            outcome.Generated.ShouldNotContain("ExceptionHandlerAdapter");
            outcome.Generated.ShouldContain("RequestExceptionProcessorBehavior<global::Ping, string>");
        }

        /// <remarks>
        /// Refused rather than skipped. The container closes an open implementation against an
        /// open service type by matching type parameters position for position, and a handler's
        /// do not line up: the request argument of IRequestHandler&lt;Ping&lt;T&gt;, T&gt; is
        /// Ping&lt;T&gt; and not T. Skipping it in silence means somebody's handler never runs
        /// and nothing ever says why.
        /// </remarks>
        [Fact]
        public void An_open_generic_handler_is_refused()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(@"
using System.Threading;
using System.Threading.Tasks;
using Mediarion;

public sealed class Wrapped<T> : IRequest<T> { }

public sealed class WrappedHandler<T> : IRequestHandler<Wrapped<T>, T>
{
    public Task<T> Handle(Wrapped<T> request, CancellationToken cancellationToken) => Task.FromResult(default(T)!);
}

[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Reported("MDR0004").ShouldBeTrue(outcome.Report());
        }

        /// <remarks>
        /// An open generic behaviour is a different shape and is supported, so it must not be
        /// caught by the same check.
        /// </remarks>
        [Fact]
        public void An_open_generic_behaviour_is_left_alone()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Handled + @"
public sealed class Around<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => next(cancellationToken);
}

[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Reported("MDR0004").ShouldBeFalse(outcome.Report());
        }

        /// <remarks>
        /// A project that wires its handlers some other way still gets the dispatch. Writing a
        /// registration against a container it does not reference would not compile.
        /// </remarks>
        [Fact]
        public void The_registration_is_left_out_where_there_is_no_container()
        {
            GeneratorOutcome outcome = GeneratorHarness.Run(Handled + @"
[GeneratedMediator]
public sealed partial class AppMediator { }");

            outcome.Generated.ShouldContain("AddAppMediator");
        }
    }
}
