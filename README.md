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
faster than MediatR because it writes the dispatch at compile time. If you are on .NET 6 or later,
are willing to change how your handlers are found, and want the speed, look at it first. This is
not a project that pretends the alternative is not there.

What is missing, and what this is for: a mediator that is a **drop-in** — change the namespace and
your handlers, behaviours and registrations compile as they are — that runs on .NET Framework 4.7.2
as well as .NET 10, and that carries no dependencies of its own.

## How much that is worth so far

`samples/Mediarion.Migration` configures one ordering layer twice over a shared domain, once on
MediatR 12.5.0 and once on Mediarion, drives both through the same script and fails when they
stop agreeing step for step. It covers a request with a response, a request without one, a
notification with two handlers, an open-generic behaviour, a closed one that short-circuits, and
a request sent as `object`.

The source of both layers is embedded in the sample and compared as text. **The whole difference
is two lines**: the `using` and the namespace. CI fails if a third ever appears.

Being clear about what that is not: matching shapes is a smaller claim than "your migration will
be easy". It says nothing about your build, your container, or thirty handlers written by
somebody who does not know both libraries.

Writing it found one real difference, since fixed. `Send(object)` on a request with no response
gave `null` here and `Unit.Value` there — the nicer answer, and the wrong one for a drop-in.

It also found the one place this parts company with MediatR on purpose.
`AutoRegisterRequestProcessors = true` registers your pre- and post-processors there and then
never calls them, because the behaviours that run them are only added when a processor is named
one at a time. Here the flag does what it says. Nobody can be relying on a setting that does
nothing, and making it work cannot break a migration that already worked.

## What it is

**0.2.0 is the current release.** It is not 1.0 for the obvious reason: nobody has used it yet.
The shape is copied from a library that has been in production for a decade, and the comparison
below is real, and neither of those is the same as somebody's application depending on it.

- `IRequest<TResponse>` and `IRequest`, with `IRequestHandler<,>` and `IRequestHandler<>`
- `INotification` and `INotificationHandler<>`, published one handler at a time or all together
- `IPipelineBehavior<,>`, running outermost first in registration order
- `IRequestPreProcessor<>` and `IRequestPostProcessor<,>`, outside the behaviours you add
- `IRequestExceptionHandler<,,>` and `IRequestExceptionAction<,>`, for answering in an
  exception's place or just recording it
- `ISender`, `IPublisher`, `IMediator`, and `AddMediarion` for the container
- Six target frameworks, from `net472` to `net10.0`, and no package dependencies in the core

Not yet: streaming requests, exception handlers, and a source generator for ahead-of-time
compilation. The last one is the reason `Mediator` is
marked `[RequiresDynamicCode]`: the run-time path closes generics over the request type, which an
application published ahead of time cannot do.

## Licence

MIT. See [LICENSE](LICENSE).
