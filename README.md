# Mediarion

An in-process mediator for CQRS on .NET, **MIT licensed**, shaped like MediatR so that moving an
existing codebase is mostly a namespace change. No licence key, no dependencies.

```csharp
public sealed class Ping : IRequest<string>
{
    public string Message { get; set; } = string.Empty;
}

public sealed class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken) =>
        Task.FromResult("pong " + request.Message);
}
```

```csharp
services.AddMediarion(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

string answer = await sender.Send(new Ping { Message = "there" });
```

```
dotnet add package Mediarion
dotnet add package Mediarion.Extensions.DependencyInjection
```

## Why this exists

MediatR **12.5.0 was the last release under plain Apache-2.0**. From 13.0.0 the package is dual
licensed — [RPL-1.5](https://opensource.org/license/rpl-1-5) or a commercial licence from Lucky
Penny Software — and **asks for a licence key at run time**. There is a free Community tier for
organisations under five million dollars of revenue, which covers a lot of people and does not
cover everybody, and which is a thing you now have to know about your employer before you can
pick a library.

Version 12.5.0 stays Apache-2.0, because a licence already granted cannot be withdrawn. That line
is frozen: no new features, no new target frameworks, no fixes.

## Where it sits

[martinothamar/Mediator](https://github.com/martinothamar/Mediator) already exists, is MIT, and is
faster than this one. If you are on .NET 6 or later, are willing to write your handlers against a
different set of interfaces, and speed is what you are choosing on, look at it first — the
[benchmarks](benchmarks/README.md) here include it, and it wins. This is not a project that
pretends the alternative is not there.

What is missing, and what this is for: a mediator that is a **drop-in** — change the namespace and
your handlers, behaviours and registrations compile as they are — that runs on .NET Framework 4.7.2
as well as .NET 10, and that carries no dependencies of its own.

## What it is

**0.6.0 is the current release.** It is not 1.0 for the obvious reason: nobody has used it yet.
The shape is copied from a library that has been in production for a decade, and the comparisons
below are real, and neither of those is the same as somebody's application depending on it.

- `IRequest<TResponse>` and `IRequest`, with `IRequestHandler<,>` and `IRequestHandler<>`
- `INotification` and `INotificationHandler<>`, published one handler at a time or all together
- `IPipelineBehavior<,>`, running outermost first in registration order
- `IRequestPreProcessor<>` and `IRequestPostProcessor<,>`, outside the behaviours you add
- `IRequestExceptionHandler<,,>` and `IRequestExceptionAction<,>`, for answering in an
  exception's place or just recording it
- `IStreamRequest<T>` and `CreateStream`, in a package of its own
- `ISender`, `IPublisher`, `IMediator`, and `AddMediarion` for the container
- A source generator, so all of it works in an application published ahead of time
- Six target frameworks, from `net472` to `net10.0`, and **no package dependencies** in the core
- The test suite **runs** on `net472` and `net48`, and is not only built for them
- The six are `netstandard2.0`, `netstandard2.1`, `net472`, `net8.0`, `net9.0` and `net10.0`.
  NuGet lists the lowest of each family and reads it as "or higher", so the three badges on
  the package page are these six: .NET 10 is inside, and a .NET Framework 4.8 application
  uses the `net472` assets.

## How much the drop-in claim is worth

`samples/Mediarion.Migration` configures one ordering layer twice over a shared domain, once on
MediatR 12.5.0 and once on Mediarion, drives both through the same script and fails when they stop
agreeing step for step. It covers a request with a response, a request without one, a notification
with two handlers, an open-generic behaviour, a closed one that short-circuits, a pre-processor, a
post-processor, an exception handler, an exception action, a stream, and a request sent as
`object`.

The source of both layers is embedded in the sample and compared as text. Every line that differs
has to be the original with the library renamed and nothing else, so **the whole difference is the
`using` lines and the namespace**. CI fails if a fourth kind of difference appears.

Being clear about what that is not: matching shapes is a smaller claim than "your migration will
be easy". It says nothing about your build, your container, or thirty handlers written by somebody
who does not know both libraries.

Writing it found real differences, since fixed — `Send(object)` on a request with no response gave
`null` here and `Unit.Value` there, and the exception behaviours sat innermost here and outermost
there.

### Where it parts company on purpose

`AutoRegisterRequestProcessors = true` registers your pre- and post-processors in MediatR and then
never calls them, because the behaviours that run them are only added when a processor is named one
at a time. Here the flag does what it says. Nobody can be relying on a setting that does nothing,
and making it work cannot break a migration that already worked.

## Used, not only tested

`samples/Mediarion.Helpdesk` is a small application that uses the library the way an application
would: a container, a pipeline with an open-generic behaviour wrapping everything and a closed one
that refuses a request before it reaches its handler, a pre-processor, a handler that publishes a
notification two handlers take, and a request with nothing to give back. It keeps a journal of
every step and fails when a line is out of place, and CI runs it.

## .NET Framework

`net472` is a target this library goes out of its way to support, and a target that is only
built for is a target nobody has run. The suite runs on `net472` and `net48` as well as the
modern three — the same 42 tests, five times — because what runs there is different code: the
`netstandard2.0` and `net472` assets, on a runtime that caches fewer tasks and gets
`IAsyncEnumerable` out of a package rather than out of itself.

Running them found nothing wrong with the library and one thing wrong with the test project,
which is roughly the expected split and still worth the six frameworks of build time.

## Ahead of time

`Mediator` works out which handler answers a request while the program runs, which means closing a
generic over a type it only learns then. An application published ahead of time cannot do that, and
the library says so rather than letting you find out on the first request: the type carries
`[RequiresDynamicCode]`.

```
dotnet add package Mediarion.SourceGenerator
```

```csharp
[GeneratedMediator]
public sealed partial class AppMediator
{
}
```

```csharp
services.AddAppMediator();
```

That is the whole of it. The generator finds every handler in the project and writes two things:
the dispatch, as a switch with one case per request, and the registration naming every handler — a
scan is reflection too. Pipeline behaviours it leaves to you, because which ones apply and in what
order is your decision and not something to guess from what happens to be lying in the project.

Your handlers, behaviours and processors are the same code either way, and so is the pipeline they
run in: it is shared between the two engines on purpose, because it contains no reflection and
sharing it is what stops them ever disagreeing about the order behaviours go in.

`samples/Mediarion.Aot` is published with `PublishAot=true` and checks its own answers. CI
publishes it natively and runs it, with the trimming and AOT analysers on, so a change that breaks
ahead-of-time fails the build rather than somebody's deployment. A test suite sends the same
requests through both engines and requires the same answers.

## Streaming

A request hands back one value once its handler has finished. A **streaming** request hands back
each value as the handler reaches it, so nothing waits for the last one and nothing holds them all
at once. Worth it for a result set too big for memory, a producer slow enough that starting early
matters, or a sequence that never ends.

```
dotnet add package Mediarion.Streaming
```

```csharp
services.AddMediarionStreaming(typeof(Program).Assembly);

await foreach (Order order in sender.CreateStream(new GetOrders()))
{
    // this one is here while the handler is still finding the next
}
```

It is a separate package for one reason: `IAsyncEnumerable<T>` is in the runtime from .NET Core 3.0
onwards and comes from a package on `netstandard2.0` and .NET Framework. Putting streaming in the
core would mean everyone carrying that package, including everyone who never streams anything.
Install this one and you take the dependency knowingly; ignore it and the core still has none.

`CreateStream` is a method on `ISender` in MediatR and an extension method on it here, because
`ISender` lives in the package with no dependencies. The call site is identical either way — the
migration sample streams on both libraries from one piece of source. What does not survive is
mocking `ISender.CreateStream`, since no mocking library can intercept an extension method: take
`IStreamSender` in the code under test and mock that.

## Open generic handlers

An **open generic handler** — `IRequestHandler<Wrapped<T>, T>` — cannot be handed to a container
as it stands. A container closes an open implementation against an open service type by matching
type parameters position for position, and a handler's do not line up: the request argument is
`Wrapped<T>` and not `T`.

Both libraries work around that the same way, and neither does it unless asked:

```csharp
cfg.RegisterGenericHandlers = true;
```

That closes the handler at registration, once per candidate type, instead of leaving it to the
container. Candidates are the concrete types of the assemblies being scanned, so
`Wrapped<Order>` finds a handler and `Wrapped<int>` does not — `int` is not a type the scan
enumerates. `MaxTypesClosing`, `MaxGenericTypeParameters`, `MaxGenericTypeRegistrations` and
`RegistrationTimeout` bound the cross product, with the same defaults as MediatR.

Without the flag the handler is skipped and the send says which request has no handler.

**Ahead of time is different.** The generated dispatch is a switch over request types known while
the project compiles, and a closed generic is not one of them, so the generator warns (`MDR0004`)
and leaves it out. There, write one closed handler per request type, or put the shared part in an
open generic `IPipelineBehavior<TRequest, TResponse>`, which is supported either way.

## Speed

Both paths beat MediatR and allocate about half of what it does; Mediator is faster than both. The
numbers, the runs they came from and what they do not flatter are in
[benchmarks/README.md](benchmarks/README.md).

## Documentation

[veneury.github.io/Mediarion](https://veneury.github.io/Mediarion/) — the articles above at
length, plus a reference for every public type generated from the XML documentation the build
requires on each one. In English and Spanish, and CI fails a build whose Spanish page was
written against an English page that has since changed.

## Licence

MIT. See [LICENSE](LICENSE).
