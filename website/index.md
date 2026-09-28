---
_layout: landing
---

# Mediarion

An in-process mediator for CQRS on .NET, MIT licensed, shaped like MediatR so that moving an
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

MediatR 12.5.0 was the last release under plain Apache-2.0. From 13.0.0 the package is dual
licensed — [RPL-1.5](https://opensource.org/license/rpl-1-5) or a commercial licence — and asks
for a licence key at run time. There is a free Community tier for organisations under five
million dollars of revenue, which covers a lot of people and does not cover everybody, and which
is a thing you now have to know about your employer before you can pick a library.

Version 12.5.0 stays Apache-2.0, because a licence already granted cannot be withdrawn. That
line is frozen: no new features, no new target frameworks, no fixes.

## Where it sits

[martinothamar/Mediator](https://github.com/martinothamar/Mediator) already exists, is MIT, and
is faster than this one. If you are on .NET 6 or later, are willing to write your handlers
against a different set of interfaces, and speed is what you are choosing on, look at it first —
the [performance page](articles/performance.md) includes it, and it wins.

What is missing, and what this is for: a mediator that is a drop-in — change the namespace and
your handlers, behaviours and registrations compile as they are — that runs on .NET Framework
4.7.2 as well as .NET 10, and that carries no dependencies of its own.

## Where to go next

- [Getting started](articles/getting-started.md) — requests, notifications and the container.
- [Migrating from MediatR](articles/migrating-from-mediatr.md) — what the drop-in claim is worth,
  and the one place the two libraries part company on purpose.
- [The pipeline](articles/pipeline.md) — behaviours, pre- and post-processors, exception
  handlers, and the order they run in.
- [Streaming](articles/streaming.md) — `IStreamRequest<T>` and `CreateStream`, in a package of
  its own.
- [Ahead-of-time and the generator](articles/aot.md) — how to publish an application with
  `PublishAot`.
- [Performance](articles/performance.md) — the numbers, the runs they came from, and where this
  library loses.
- [API reference](api/Mediarion.yml) — every public type, generated from the XML documentation
  the build requires on each one.
