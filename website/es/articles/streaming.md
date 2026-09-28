# Streaming

Un request devuelve un valor cuando su handler ha terminado. Un request de **streaming** devuelve
cada valor según el handler llega a él, así que nada espera al último y nada los sostiene todos a
la vez.

Vale la pena para un conjunto de resultados que no cabe en memoria, para un productor lo bastante
lento como para que empezar antes importe, o para una secuencia que no termina nunca. No vale la
pena para una lista de cuarenta cosas.

```
dotnet add package Mediarion.Streaming
```

## Escribir uno

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

## Registrar y enviar

```csharp
services.AddMediarion(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
services.AddMediarionStreaming(typeof(Program).Assembly);
```

`AddMediarionStreaming` busca handlers y behaviours de streaming, y reemplaza el mediador
registrado por uno que sabe hacer streaming. Llámalo **después** de `AddMediarion`.

```csharp
await foreach (Order order in sender.CreateStream(new GetOrders { CustomerId = 7 }))
{
    // este ya está aquí mientras el handler todavía busca el siguiente
}
```

Salir del bucle detiene al handler. Cancelar el token también.

## Behaviours

`IStreamPipelineBehavior<TRequest, TResponse>` es la contraparte de `IPipelineBehavior<,>` para
streaming, y ve cada valor de salida en lugar de una respuesta al final.

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

        // aquí se conoce count, una vez que el stream ha terminado
    }
}
```

`StreamHandlerDelegate<TResponse>` no recibe argumentos, que es la forma que usa MediatR: la
secuencia que devuelve ya está atada al token con el que se llamó al handler, y `WithCancellation`
es cómo detienes tu propio bucle sobre ella.

Registra uno con `AddStreamBehavior`, que es una extensión sobre la colección de servicios y no
sobre la configuración, porque el streaming se configura después de que `AddMediarion` haya
corrido:

```csharp
services.AddStreamBehavior(typeof(CountStreamed<,>));
```

El `IPipelineBehavior<,>` de siempre no aplica a un stream y no corre para uno. Los dos pipelines
están separados porque las formas lo están: uno devuelve una respuesta, el otro devuelve una
secuencia.

## Por qué es un paquete aparte

`IAsyncEnumerable<T>` está en el runtime desde .NET Core 3.0, y viene de
`Microsoft.Bcl.AsyncInterfaces` en `netstandard2.0` y en .NET Framework. Poner el streaming en el
core significaría que todo el mundo cargue ese paquete, incluida toda la gente que no hace
streaming de nada. Instala este y tomas la dependencia a sabiendas; ignóralo y el core sigue sin
ninguna.

## Lo único que no sobrevive a una migración

`CreateStream` es un **método de `ISender`** en MediatR y un **método de extensión** aquí, por la
misma razón por la que el paquete está separado: `ISender` vive en el paquete sin dependencias.

El sitio de llamada es idéntico — el sample de migración hace streaming sobre las dos bibliotecas
desde un solo trozo de código. Lo que no sobrevive es **hacer un mock de `ISender.CreateStream`**,
porque ninguna biblioteca de mocking puede interceptar un método de extensión.

Toma `IStreamSender` en el código bajo prueba y haz el mock de eso:

```csharp
public sealed class OrderReport
{
    private readonly IStreamSender sender;

    public OrderReport(IStreamSender sender) => this.sender = sender;
}
```

`IStreamSender` lo registra `AddMediarionStreaming`, y el mediador que registra implementa esa
interfaz y `ISender`, así que no se duplica nada.

Si el sender que tienes no puede hacer streaming — porque `AddMediarionStreaming` nunca se llamó,
o se llamó antes de `AddMediarion` y quedó sobrescrito — `CreateStream` lanza una
`MediarionException` que nombra el tipo y dice qué hay que llamar, en vez de fallar un cast.
