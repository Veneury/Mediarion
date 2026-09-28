# Migrating from MediatR

## The short version

Change the namespace and the registration call. Your handlers, behaviours, processors and
exception handlers compile as they are.

```diff
-using MediatR;
+using Mediarion;
```

```diff
-services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
+services.AddMediarion(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
```

```
dotnet remove package MediatR
dotnet add package Mediarion
dotnet add package Mediarion.Extensions.DependencyInjection
```

Everything below is the detail behind that, and the places it is not quite true.

## What the claim is worth

`samples/Mediarion.Migration` in the repository configures **one ordering layer twice** over a
shared domain — once on MediatR 12.5.0 and once on Mediarion — drives both through the same
script, and fails when they stop agreeing step for step. It covers a request with a response, a
request without one, a notification with two handlers, an open-generic behaviour, a closed one
that short-circuits, a pre-processor, a post-processor, an exception handler, an exception
action, a stream, and a request sent as `object`.

The source of both layers is embedded in the sample and compared as text. **Every line that
differs has to be the original with the library renamed and nothing else**, so the whole
difference is the `using` lines and the namespace. CI fails if a fourth kind of difference
appears.

Being clear about what that is not: matching shapes is a smaller claim than "your migration will
be easy". It says nothing about your build, your container, or thirty handlers written by
somebody who knows neither library well.

Writing it found real differences, since fixed. `Send(object)` on a request with no response gave
`null` here and `Unit.Value` there. The exception behaviours sat innermost here and outermost
there. Both were found by the sample and not by a test, because a test would have asserted
whatever the library already did.

## The names

| MediatR | Mediarion |
|---|---|
| `IRequest<T>`, `IRequest` | the same |
| `IRequestHandler<,>`, `IRequestHandler<>` | the same |
| `INotification`, `INotificationHandler<>` | the same |
| `IPipelineBehavior<,>`, `RequestHandlerDelegate<T>` | the same |
| `IRequestPreProcessor<>`, `IRequestPostProcessor<,>` | the same |
| `IRequestExceptionHandler<,,>`, `IRequestExceptionAction<,>` | the same |
| `ISender`, `IPublisher`, `IMediator`, `Unit` | the same |
| `MediatorServiceConfiguration` | `MediarionServiceConfiguration` |
| `AddMediatR(...)` | `AddMediarion(...)` |
| `sender.CreateStream(...)` | the same call, [an extension method](streaming.md) |

The namespace `MediatR.Pipeline` becomes `Mediarion.Pipeline`, and holds the same types.

## Where it parts company on purpose

`AutoRegisterRequestProcessors = true` registers your pre- and post-processors in MediatR **and
then never calls them**, because the behaviours that run them are only added when a processor is
named one at a time. Here the flag does what it says.

That was found by writing the migration sample, not by reading the source: the pre-processor
simply did not run. Nobody can be relying on a setting that does nothing, and making it work
cannot break a migration that already worked.

## Where it cannot follow

**Mocking `ISender.CreateStream`.** `CreateStream` is a method on `ISender` over there and an
extension method here, because `ISender` lives in the package with no dependencies and streaming
needs `IAsyncEnumerable`. The call site is identical; a mocking library cannot intercept an
extension method. Take `IStreamSender` in the code under test and mock that. See
[streaming](streaming.md).

**A licence key.** There is none to configure, and nothing reads one.

## What to check after the change

1. It builds. If it does not, the error names a type, and the table above says what it is now.
2. The behaviour order is the order you registered them in, outermost first — the same rule as
   before.
3. If you used `AutoRegisterRequestProcessors`, your processors now run. That is a change in what
   the application does, even though nothing in your code changed.
4. If you publish [ahead of time](aot.md), add `Mediarion.SourceGenerator`. The run-time mediator
   closes a generic over a type it learns while running, which a native image cannot do, and the
   type is marked `[RequiresDynamicCode]` so the build tells you rather than the first request.
