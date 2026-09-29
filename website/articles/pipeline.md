# The pipeline

Some things belong around every request rather than inside each handler: a log line, a
validation, a transaction, a metric, an exception nobody wants to catch twenty times. The
pipeline is where they go.

## A behaviour

`IPipelineBehavior<TRequest, TResponse>` wraps the handler. It gets the request, a delegate that
carries on down the chain, and the cancellation token.

```csharp
public sealed class Logged<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<Logged<TRequest, TResponse>> log;

    public Logged(ILogger<Logged<TRequest, TResponse>> log) => this.log = log;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        log.LogInformation("Handling {Request}", typeof(TRequest).Name);
        TResponse response = await next(cancellationToken);
        log.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}
```

Not calling `next` short-circuits: nothing further runs and the handler is never reached. That is
how a validation refuses a request, and it is a normal thing for a behaviour to do.

Register an open generic one with `AddOpenBehavior`, and a closed one with `AddBehavior`:

```csharp
services.AddMediarion(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddOpenBehavior(typeof(Logged<,>));
    cfg.AddBehavior<ValidatePlaceOrder>();
});
```

**Order is registration order, outermost first.** The behaviour added first sees the request
first and the response last. Nothing is inferred from the type name or from what is lying around
in the assembly, because which behaviours apply and in what order is a decision, not something to
guess at.

## Pre- and post-processors

A behaviour that only wants to run before, or only after, can be written as one of these instead.
They are smaller and they say what they are.

```csharp
public sealed class StampReceived : IRequestPreProcessor<PlaceOrder>
{
    public Task Process(PlaceOrder request, CancellationToken cancellationToken)
    {
        request.ReceivedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

public sealed class CountPlaced : IRequestPostProcessor<PlaceOrder, int>
{
    public Task Process(PlaceOrder request, int response, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
```

They run **outside** the behaviours you add: every pre-processor first, then your behaviours,
then the handler, then every post-processor.

Name them one at a time, or turn the whole scan on:

```csharp
cfg.AddRequestPreProcessor<StampReceived>();
cfg.AddRequestPostProcessor<CountPlaced>();

// or
cfg.AutoRegisterRequestProcessors = true;
```

> `AutoRegisterRequestProcessors` is [the one place this library deliberately parts company with
> MediatR](migrating-from-mediatr.md#where-it-parts-company-on-purpose): over there the flag
> registers your processors and then never calls them. Here it does what it says.

If there are no processors at all, neither of the two behaviours that run them is registered.
That is not a tidiness point. Registering them unconditionally cost 110 nanoseconds and 480 bytes
on **every** request in **every** application, because an open-generic registration is closed per
request-and-response pair and each of those behaviours asks the container for an enumerable of
its own. See [performance](performance.md).

## Exception handlers

`IRequestExceptionHandler<TRequest, TResponse, TException>` gets a chance to answer in the
exception's place.

```csharp
public sealed class OrderMissing
    : IRequestExceptionHandler<GetOrder, Order, OrderNotFoundException>
{
    public Task Handle(
        GetOrder request,
        OrderNotFoundException exception,
        RequestExceptionHandlerState<Order> state,
        CancellationToken cancellationToken)
    {
        state.SetHandled(Order.Empty);
        return Task.CompletedTask;
    }
}
```

Call `SetHandled` and the request completes with that response as though nothing had gone wrong.
Do not, and the exception carries on up.

`IRequestExceptionAction<TRequest, TException>` is the other half: it runs on the way past and
cannot answer. That is the one to use for recording something, because it cannot accidentally
swallow the failure.

```csharp
public sealed class RecordFailure : IRequestExceptionAction<GetOrder, Exception>
{
    public Task Execute(GetOrder request, Exception exception, CancellationToken cancellationToken)
    {
        // count it, log it, trace it
        return Task.CompletedTask;
    }
}
```

Both are found by the assembly scan and need no registration of their own.

**They sit outermost**, outside the pre-processors and outside your behaviours, so an exception
thrown anywhere in the pipeline reaches them. That placement is not a guess: the [migration
sample](migrating-from-mediatr.md) drives the same script through both libraries and failed until
it matched.

## The whole order

From the outside in:

1. Exception handlers and exception actions
2. Pre-processors
3. Post-processors
4. Your behaviours, in registration order
5. The handler

## Open generic handlers

An **open generic handler** — `IRequestHandler<Wrapped<T>, T>` — cannot be handed to a container
as it stands. A container closes an open implementation against an open service type by matching
type parameters position for position, and a handler's do not line up: the request argument is
`Wrapped<T>` and not `T`.

Both libraries work around that the same way, and neither does it unless asked:

```csharp
services.AddMediarion(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.RegisterGenericHandlers = true;
});
```

The closing is done at registration, once per candidate type, rather than left to the container.
Candidates are the concrete types of the assemblies being scanned, so `Wrapped<Order>` finds a
handler and `Wrapped<int>` does not: `int` is not a type the scan enumerates, and closing over
every type the runtime can name is not a finite job.

Four settings bound the cross product, with MediatR's defaults:

| | |
|---|---|
| `MaxTypesClosing` | how many types one handler may be closed over (100) |
| `MaxGenericTypeParameters` | how many type parameters a handler may have before it is left alone (10) |
| `MaxGenericTypeRegistrations` | how many closed handlers may be registered in total (125,000) |
| `RegistrationTimeout` | how long the closing may take, in milliseconds (15,000) |

Without the flag the handler is skipped and the send says which request has no handler.

**[Ahead of time](aot.md) is different.** The generated dispatch is a switch over request types
known while the project compiles, and a closed generic is not one of them, so the generator warns
(`MDR0004`) and leaves it out. There, write one closed handler per request type, or put the shared
part in an open generic `IPipelineBehavior<TRequest, TResponse>` — which **is** supported either
way, and is usually what the generic handler was reaching for.
