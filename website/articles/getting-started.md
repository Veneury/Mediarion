# Getting started

## Install

```
dotnet add package Mediarion
dotnet add package Mediarion.Extensions.DependencyInjection
```

The first package is the contracts and the run-time mediator, and it has **no dependencies at
all**. The second is `AddMediarion` and the assembly scan, and it depends only on
`Microsoft.Extensions.DependencyInjection.Abstractions`. Two more packages exist and neither is
needed to start: [`Mediarion.Streaming`](streaming.md) and
[`Mediarion.SourceGenerator`](aot.md).

Six target frameworks are built, from `net472` to `net10.0`.

## A request

A request is a message with exactly one handler. It carries what the handler needs and says what
comes back.

```csharp
public sealed class GetOrder : IRequest<Order>
{
    public int Id { get; set; }
}

public sealed class GetOrderHandler : IRequestHandler<GetOrder, Order>
{
    private readonly IOrderStore store;

    public GetOrderHandler(IOrderStore store) => this.store = store;

    public Task<Order> Handle(GetOrder request, CancellationToken cancellationToken) =>
        store.Find(request.Id, cancellationToken);
}
```

A request with nothing to give back implements `IRequest`, and its handler
`IRequestHandler<TRequest>`:

```csharp
public sealed class CancelOrder : IRequest
{
    public int Id { get; set; }
}

public sealed class CancelOrderHandler : IRequestHandler<CancelOrder>
{
    public Task Handle(CancelOrder request, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
```

## A notification

A notification is a message with any number of handlers, including none. Nothing comes back from
one, which is the difference that matters: a request asks, a notification tells.

```csharp
public sealed class OrderPlaced : INotification
{
    public int Id { get; set; }
}

public sealed class SendReceipt : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

public sealed class UpdateStock : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
```

By default the handlers run one after another and the first exception stops the rest. Pass
`NotificationPublishers.TaskWhenAllPublisher` if you would rather start them all and wait:

```csharp
services.AddMediarion(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.NotificationPublisher = new NotificationPublishers.TaskWhenAllPublisher();
});
```

## The container

```csharp
services.AddMediarion(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
```

That scans the assembly for handlers, notification handlers, processors and exception handlers,
registers each one, and registers `ISender`, `IPublisher` and `IMediator`. Several assemblies at
once:

```csharp
services.AddMediarion(cfg => cfg.RegisterServicesFromAssemblies(
    typeof(Program).Assembly,
    typeof(SomethingInAnotherProject).Assembly));
```

Everything is registered `Transient` by default. `cfg.Lifetime` changes that for the handlers and
the mediator together.

## Sending

Take `ISender` where you send and `IPublisher` where you publish. `IMediator` is both, and is
there because MediatR has it; the narrower two are usually what a class actually needs.

```csharp
public sealed class OrdersController : ControllerBase
{
    private readonly ISender sender;

    public OrdersController(ISender sender) => this.sender = sender;

    [HttpGet("{id}")]
    public async Task<Order> Get(int id, CancellationToken cancellationToken) =>
        await sender.Send(new GetOrder { Id = id }, cancellationToken);
}
```

`Send` also takes an `object`, for when the request type is only known at run time — a message
off a queue, say. It gives back `Task<object?>`, and a request with no response gives back
`Unit.Value` rather than `null`, because that is what MediatR does.

## What comes next

Everything above is the whole of the everyday surface. The rest is optional:

- [The pipeline](pipeline.md), for the things that should happen around every request rather than
  inside each handler.
- [Streaming](streaming.md), for a response that arrives in pieces.
- [Ahead-of-time and the generator](aot.md), for an application published with `PublishAot`.
- [Migrating from MediatR](migrating-from-mediatr.md), if you are coming from there.
