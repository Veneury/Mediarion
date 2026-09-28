# Ahead-of-time and the generator

## The problem

The run-time mediator works out which handler answers a request while the program is running,
which means closing a generic over a type it only learns then. An application published ahead of
time cannot do that: there is no JIT to build the closed type.

The library says so rather than letting you find out on the first request — `Mediator` carries
`[RequiresDynamicCode]`, so a project with the AOT analyser on gets a build warning at the call,
not a crash in production.

## The answer

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

- **the dispatch**, as a `switch` with one case per request type, so nothing is closed at run
  time;
- **the registration**, naming every handler, because a scan is reflection too.

`AddAppMediator` is generated from the class name, so a mediator called `OrderMediator` gets
`AddOrderMediator`.

## What it does not do

**Pipeline behaviours are left to you.** Which behaviours apply and in what order is a decision,
and it is not something to guess from what happens to be lying in the project. Register them
closed, the way an application published ahead of time has to anyway:

```csharp
services.AddTransient<IPipelineBehavior<PlaceOrder, int>, Logged<PlaceOrder, int>>();
```

Pre- and post-processors are registered when `[GeneratedMediator(RegisterRequestProcessors = true)]`
says so, which mirrors `AutoRegisterRequestProcessors` on the run-time side.

## Your code does not change

Handlers, behaviours and processors are the same code either way, and so is the pipeline they run
in. That pipeline is **shared between the two engines on purpose**: it contains no reflection, and
sharing it is what stops them ever disagreeing about the order behaviours go in. A test suite
sends the same requests through both and requires the same answers.

## Diagnostics

The generator says what it cannot do, at the declaration, while the project compiles.

| | |
|---|---|
| `MDR0001` | The class marked `[GeneratedMediator]` is not `partial`, so the dispatch has nowhere to go. **Error.** |
| `MDR0002` | Two handlers answer one request. A request has one handler; use a notification for the other. **Error.** |
| `MDR0003` | A request has no handler in this project, so sending it will fail at run time unless one is registered from somewhere else. **Warning**, because the handler may legitimately live in another assembly. |
| `MDR0004` | An open generic handler, which [no container can close](pipeline.md#open-generic-handlers). **Error.** |

## Checked, not assumed

`samples/Mediarion.Aot` in the repository is published with `PublishAot=true` and checks its own
answers: requests, notifications, an exception handler and a stream. CI publishes it natively and
runs it, with the trimming and AOT analysers on, so a change that breaks ahead-of-time fails the
build rather than somebody's deployment.
