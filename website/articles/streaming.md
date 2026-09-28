# Streaming

A request hands back one value once its handler has finished. A **streaming** request hands back
each value as the handler reaches it, so nothing waits for the last one and nothing holds them
all at once.

Worth it for a result set too big for memory, a producer slow enough that starting early matters,
or a sequence that never ends. Not worth it for a list of forty things.

```
dotnet add package Mediarion.Streaming
```

## Writing one

```csharp
public sealed class GetOrders : IStreamRequest<Order>
{
    public int CustomerId { get; set; }
}

public sealed class GetOrdersHandler : IStreamRequestHandler<GetOrders, Order>
{
    private readonly IOrderStore store;

    public GetOrdersHandler(IOrderStore store) => this.store = store;

    public async IAsyncEnumerable<Order> Handle(
        GetOrders request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (Order order in store.For(request.CustomerId, cancellationToken))
        {
            yield return order;
        }
    }
}
```

## Registering and sending

```csharp
services.AddMediarion(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
services.AddMediarionStreaming(typeof(Program).Assembly);
```

`AddMediarionStreaming` scans for stream handlers and stream behaviours, and replaces the
registered mediator with one that can stream. Call it **after** `AddMediarion`.

```csharp
await foreach (Order order in sender.CreateStream(new GetOrders { CustomerId = 7 }))
{
    // this one is here while the handler is still finding the next
}
```

Breaking out of the loop stops the handler. So does cancelling the token.

## Behaviours

`IStreamPipelineBehavior<TRequest, TResponse>` is the streaming counterpart of
`IPipelineBehavior<,>`, and sees every value on its way out rather than one response at the end.

```csharp
public sealed class CountStreamed<TRequest, TResponse>
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int count = 0;

        await foreach (TResponse value in next().WithCancellation(cancellationToken))
        {
            count++;
            yield return value;
        }

        // count is known here, once the stream has finished
    }
}
```

`StreamHandlerDelegate<TResponse>` takes no arguments, which is the shape MediatR uses: the
sequence it gives back is already bound to the token the handler was called with, and
`WithCancellation` is how you stop your own loop over it.

Register one with `AddStreamBehavior`, which is an extension on the service collection rather
than on the configuration, because streaming is configured after `AddMediarion` has run:

```csharp
services.AddStreamBehavior(typeof(CountStreamed<,>));
```

The ordinary `IPipelineBehavior<,>` does not apply to a stream and is not run for one. The two
pipelines are separate because the shapes are: one returns a response, the other returns a
sequence.

## Why it is a separate package

`IAsyncEnumerable<T>` is in the runtime from .NET Core 3.0 onwards, and comes from
`Microsoft.Bcl.AsyncInterfaces` on `netstandard2.0` and .NET Framework. Putting streaming in the
core would mean everyone carrying that package, including everyone who never streams anything.
Install this one and you take the dependency knowingly; ignore it and the core still has none.

## The one thing that does not survive a migration

`CreateStream` is a **method on `ISender`** in MediatR and an **extension method** on it here,
for the same reason the package is separate: `ISender` lives in the package with no dependencies.

The call site is identical either way — the migration sample streams on both libraries from one
piece of source. What does not survive is **mocking `ISender.CreateStream`**, because no mocking
library can intercept an extension method.

Take `IStreamSender` in the code under test and mock that:

```csharp
public sealed class OrderReport
{
    private readonly IStreamSender sender;

    public OrderReport(IStreamSender sender) => this.sender = sender;
}
```

`IStreamSender` is registered by `AddMediarionStreaming`, and the mediator it registers implements
both it and `ISender`, so nothing is duplicated.

If the sender you hold cannot stream — because `AddMediarionStreaming` was never called, or was
called before `AddMediarion` and overwritten — `CreateStream` throws a `MediarionException`
naming the type and saying what to call, rather than failing a cast.
