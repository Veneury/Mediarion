# El pipeline

Algunas cosas van alrededor de cada request y no dentro de cada handler: una línea de log, una
validación, una transacción, una métrica, una excepción que nadie quiere capturar veinte veces.
El pipeline es donde van.

## Un behaviour

`IPipelineBehavior<TRequest, TResponse>` envuelve al handler. Recibe el request, un delegado que
sigue la cadena hacia dentro, y el token de cancelación.

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

No llamar a `next` corta el circuito: no corre nada más adentro y el handler nunca se alcanza. Así
es como una validación rechaza un request, y es algo normal que haga un behaviour.

Registra uno genérico abierto con `AddOpenBehavior`, y uno cerrado con `AddBehavior`:

```csharp
services.AddMediarion(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddOpenBehavior(typeof(Logged<,>));
    cfg.AddBehavior<ValidatePlaceOrder>();
});
```

**El orden es el orden de registro, de fuera hacia dentro.** El behaviour registrado primero ve el
request primero y la respuesta último. Nada se deduce del nombre del tipo ni de lo que haya tirado
por el ensamblado, porque qué behaviours aplican y en qué orden es una decisión, no algo que
adivinar.

## Pre y post procesadores

Un behaviour que solo quiere correr antes, o solo después, se puede escribir como uno de estos.
Son más pequeños y dicen lo que son.

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

Corren **por fuera** de los behaviours que tú registras: primero todos los pre procesadores,
después tus behaviours, después el handler, después todos los post procesadores.

Nómbralos uno a uno, o enciende el escaneo entero:

```csharp
cfg.AddRequestPreProcessor<StampReceived>();
cfg.AddRequestPostProcessor<CountPlaced>();

// o bien
cfg.AutoRegisterRequestProcessors = true;
```

> `AutoRegisterRequestProcessors` es [el único punto donde esta biblioteca se separa de MediatR a
> propósito](migrating-from-mediatr.md#dónde-se-separa-a-propósito): allá la bandera registra tus
> procesadores y luego no los llama nunca. Aquí hace lo que dice.

Si no hay ningún procesador, ninguno de los dos behaviours que los ejecutan se registra. Eso no es
una cuestión de limpieza. Registrarlos siempre costaba 110 nanosegundos y 480 bytes en **cada**
request de **cada** aplicación, porque un registro genérico abierto se cierra por cada par de
request y respuesta, y cada uno de esos behaviours le pide al contenedor un enumerable propio.
Ver [rendimiento](performance.md).

## Manejadores de excepciones

`IRequestExceptionHandler<TRequest, TResponse, TException>` tiene la oportunidad de responder en
lugar de la excepción.

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

Llama a `SetHandled` y el request termina con esa respuesta como si no hubiera pasado nada. Si no
lo llamas, la excepción sigue subiendo.

`IRequestExceptionAction<TRequest, TException>` es la otra mitad: corre al pasar y no puede
responder. Es el que hay que usar para registrar algo, porque no puede tragarse el fallo sin
querer.

```csharp
public sealed class RecordFailure : IRequestExceptionAction<GetOrder, Exception>
{
    public Task Execute(GetOrder request, Exception exception, CancellationToken cancellationToken)
    {
        // contarlo, loguearlo, trazarlo
        return Task.CompletedTask;
    }
}
```

Los dos los encuentra el escaneo de ensamblados y no necesitan registro propio.

**Van por fuera de todo**, por fuera de los pre procesadores y por fuera de tus behaviours, para
que una excepción lanzada en cualquier punto del pipeline llegue a ellos. Esa posición no es una
suposición: el [sample de migración](migrating-from-mediatr.md) pasa el mismo guion por las dos
bibliotecas y fallaba hasta que coincidió.

## El orden completo

De fuera hacia dentro:

1. Manejadores y acciones de excepciones
2. Pre procesadores
3. Post procesadores
4. Tus behaviours, en orden de registro
5. El handler

## Handlers genéricos abiertos

Un **handler genérico abierto** — `IRequestHandler<Wrapped<T>, T>` — no se le puede entregar a un
contenedor tal cual. Un contenedor cierra una implementación abierta contra un tipo de servicio
abierto emparejando los parámetros de tipo posición por posición, y los de un handler no cuadran:
el argumento del request es `Wrapped<T>` y no `T`.

Las dos bibliotecas lo rodean igual, y ninguna lo hace si no se lo pides:

```csharp
services.AddMediarion(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.RegisterGenericHandlers = true;
});
```

El cierre se hace en el registro, una vez por cada tipo candidato, en lugar de dejárselo al
contenedor. Los candidatos son los tipos concretos de los ensamblados que se recorren, así que
`Wrapped<Order>` encuentra handler y `Wrapped<int>` no: `int` no es un tipo que el escaneo
enumere, y cerrar sobre todos los tipos que el runtime puede nombrar no es un trabajo finito.

Cuatro ajustes acotan el producto cartesiano, con los valores por defecto de MediatR:

| | |
|---|---|
| `MaxTypesClosing` | sobre cuántos tipos se puede cerrar un handler (100) |
| `MaxGenericTypeParameters` | cuántos parámetros de tipo puede tener antes de dejarlo estar (10) |
| `MaxGenericTypeRegistrations` | cuántos handlers cerrados se pueden registrar en total (125.000) |
| `RegistrationTimeout` | cuánto puede durar el cierre, en milisegundos (15.000) |

Sin la bandera el handler se salta y el envío dice qué request no tiene handler.

**[Ahead of time](aot.md) es distinto.** El despacho generado es un `switch` sobre tipos de request
conocidos al compilar, y un genérico cerrado no es uno de ellos, así que el generador avisa
(`MDR0004`) y lo deja fuera. Ahí, escribe un handler cerrado por cada tipo de request, o pon la
parte compartida en un `IPipelineBehavior<TRequest, TResponse>` genérico abierto — que **sí** está
soportado de las dos maneras, y suele ser lo que el handler genérico buscaba.
