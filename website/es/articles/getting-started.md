# Primeros pasos

## Instalación

```
dotnet add package Mediarion
dotnet add package Mediarion.Extensions.DependencyInjection
```

El primer paquete son los contratos y el mediador en tiempo de ejecución, y **no tiene ninguna
dependencia**. El segundo es `AddMediarion` y el escaneo de ensamblados, y solo depende de
`Microsoft.Extensions.DependencyInjection.Abstractions`. Existen dos paquetes más y ninguno hace
falta para empezar: [`Mediarion.Streaming`](streaming.md) y
[`Mediarion.SourceGenerator`](aot.md).

Se compilan seis frameworks de destino, desde `net472` hasta `net10.0`.

## Un request

Un request es un mensaje con exactamente un handler. Lleva lo que el handler necesita y dice qué
devuelve.

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

Un request que no devuelve nada implementa `IRequest`, y su handler
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

## Una notificación

Una notificación es un mensaje con cualquier cantidad de handlers, incluida ninguna. De una
notificación no vuelve nada, que es la diferencia que importa: un request pregunta, una
notificación avisa.

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

Por defecto los handlers corren uno tras otro y la primera excepción detiene el resto. Pasa
`NotificationPublishers.TaskWhenAllPublisher` si prefieres arrancarlos todos y esperar:

```csharp
services.AddMediarion(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.NotificationPublisher = new NotificationPublishers.TaskWhenAllPublisher();
});
```

## El contenedor

```csharp
services.AddMediarion(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
```

Eso recorre el ensamblado buscando handlers, handlers de notificaciones, procesadores y
manejadores de excepciones, registra cada uno, y registra `ISender`, `IPublisher` e `IMediator`.
Varios ensamblados a la vez:

```csharp
services.AddMediarion(cfg => cfg.RegisterServicesFromAssemblies(
    typeof(Program).Assembly,
    typeof(SomethingInAnotherProject).Assembly));
```

Todo se registra como `Transient` por defecto. `cfg.Lifetime` cambia eso para los handlers y el
mediador a la vez.

## Enviar

Toma `ISender` donde envíes e `IPublisher` donde publiques. `IMediator` es las dos cosas, y está
porque MediatR lo tiene; las dos interfaces más estrechas suelen ser lo que una clase realmente
necesita.

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

`Send` también acepta un `object`, para cuando el tipo del request solo se conoce en tiempo de
ejecución — un mensaje sacado de una cola, por ejemplo. Devuelve `Task<object?>`, y un request sin
respuesta devuelve `Unit.Value` en lugar de `null`, porque es lo que hace MediatR.

## Qué viene después

Todo lo anterior es la superficie completa del día a día. El resto es opcional:

- [El pipeline](pipeline.md), para lo que debería ocurrir alrededor de cada request en lugar de
  dentro de cada handler.
- [Streaming](streaming.md), para una respuesta que llega por partes.
- [Ahead-of-time y el generador](aot.md), para una aplicación publicada con `PublishAot`.
- [Migrar desde MediatR](migrating-from-mediatr.md), si vienes de ahí.
