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

## What it is, so far

**0.1 is not released.** It is being built in the open and the shape below is what works today.

- `IRequest<TResponse>` and `IRequest`, with `IRequestHandler<,>` and `IRequestHandler<>`
- `INotification` and `INotificationHandler<>`, published one handler at a time or all together
- `IPipelineBehavior<,>`, running outermost first in registration order
- `ISender`, `IPublisher`, `IMediator`, and `AddMediarion` for the container
- Six target frameworks, from `net472` to `net10.0`, and no package dependencies in the core

Not yet: streaming requests, pre- and post-processors wired into the pipeline, exception handlers,
and a source generator for ahead-of-time compilation. The last one is the reason `Mediator` is
marked `[RequiresDynamicCode]`: the run-time path closes generics over the request type, which an
application published ahead of time cannot do.

## Licence

MIT. See [LICENSE](LICENSE).
