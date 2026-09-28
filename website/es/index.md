---
_layout: landing
---

# Mediarion

Un mediador en proceso para CQRS en .NET, con licencia MIT, con la misma forma que MediatR para
que mover una base de código existente sea básicamente un cambio de namespace. Sin clave de
licencia y sin dependencias.

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

## Por qué existe

MediatR 12.5.0 fue la última versión bajo Apache-2.0 a secas. Desde la 13.0.0 el paquete tiene
licencia dual — [RPL-1.5](https://opensource.org/license/rpl-1-5) o una licencia comercial — y
pide una clave de licencia en tiempo de ejecución. Hay un nivel Community gratuito para
organizaciones por debajo de cinco millones de dólares de ingresos, que cubre a mucha gente y no
cubre a todo el mundo, y que es algo que ahora tienes que saber sobre tu empresa antes de poder
elegir una biblioteca.

La versión 12.5.0 sigue siendo Apache-2.0, porque una licencia ya concedida no se puede retirar.
Esa línea está congelada: sin funciones nuevas, sin frameworks nuevos, sin correcciones.

## Dónde encaja

[martinothamar/Mediator](https://github.com/martinothamar/Mediator) ya existe, es MIT y es más
rápido que este. Si estás en .NET 6 o posterior, estás dispuesto a escribir tus handlers contra
otro conjunto de interfaces y lo que decide es la velocidad, míralo primero — la [página de
rendimiento](articles/performance.md) lo incluye, y gana.

Lo que falta, y para lo que existe esto: un mediador que sea un reemplazo directo — cambias el
namespace y tus handlers, behaviours y registros compilan tal cual — que corra en .NET Framework
4.7.2 igual que en .NET 10, y que no arrastre dependencias propias.

## Por dónde seguir

- [Primeros pasos](articles/getting-started.md) — requests, notificaciones y el contenedor.
- [Migrar desde MediatR](articles/migrating-from-mediatr.md) — cuánto vale la promesa de
  reemplazo directo, y el único punto donde las dos bibliotecas se separan a propósito.
- [El pipeline](articles/pipeline.md) — behaviours, pre y post procesadores, manejadores de
  excepciones y el orden en que corren.
- [Streaming](articles/streaming.md) — `IStreamRequest<T>` y `CreateStream`, en un paquete
  aparte.
- [Ahead-of-time y el generador](articles/aot.md) — cómo publicar una aplicación con
  `PublishAot`.
- [Rendimiento](articles/performance.md) — los números, las corridas de las que salen y dónde
  esta biblioteca pierde.
- [Referencia de la API](../api/Mediarion.yml) — todos los tipos públicos, generados a partir de
  la documentación XML que la compilación exige en cada uno.
