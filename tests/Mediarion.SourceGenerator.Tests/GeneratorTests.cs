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
