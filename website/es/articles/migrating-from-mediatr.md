# Migrar desde MediatR

## La versión corta

Cambia el namespace y la llamada de registro. Tus handlers, behaviours, procesadores y manejadores
de excepciones compilan tal cual.

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

Todo lo que sigue es el detalle detrás de eso, y los puntos donde no es del todo cierto.

## Cuánto vale la promesa

`samples/Mediarion.Migration`, en el repositorio, configura **una misma capa de pedidos dos
veces** sobre un dominio compartido — una vez sobre MediatR 12.5.0 y otra sobre Mediarion —, pasa
las dos por el mismo guion y falla cuando dejan de coincidir paso por paso. Cubre un request con
respuesta, uno sin ella, una notificación con dos handlers, un behaviour genérico abierto, uno
cerrado que corta el circuito, un pre procesador, un post procesador, un manejador de excepciones,
una acción de excepción, un stream y un request enviado como `object`.

El código fuente de las dos capas va embebido en el sample y se compara como texto. **Cada línea
que difiere tiene que ser la original con la biblioteca renombrada y nada más**, así que toda la
diferencia son las líneas `using` y el namespace. CI falla si aparece un cuarto tipo de
diferencia.

Siendo claros sobre lo que eso no es: que las formas coincidan es una promesa más pequeña que "tu
migración será fácil". No dice nada de tu compilación, de tu contenedor, ni de treinta handlers
escritos por alguien que no conoce bien ninguna de las dos bibliotecas.

Escribirlo encontró diferencias reales, ya corregidas. `Send(object)` sobre un request sin
respuesta devolvía `null` aquí y `Unit.Value` allá. Los behaviours de excepciones estaban por
dentro aquí y por fuera allá. Las dos las encontró el sample y no una prueba, porque una prueba
habría afirmado lo que la biblioteca ya hacía.

## Los nombres

| MediatR | Mediarion |
|---|---|
| `IRequest<T>`, `IRequest` | igual |
| `IRequestHandler<,>`, `IRequestHandler<>` | igual |
| `INotification`, `INotificationHandler<>` | igual |
| `IPipelineBehavior<,>`, `RequestHandlerDelegate<T>` | igual |
| `IRequestPreProcessor<>`, `IRequestPostProcessor<,>` | igual |
| `IRequestExceptionHandler<,,>`, `IRequestExceptionAction<,>` | igual |
| `ISender`, `IPublisher`, `IMediator`, `Unit` | igual |
| `MediatorServiceConfiguration` | `MediarionServiceConfiguration` |
| `AddMediatR(...)` | `AddMediarion(...)` |
| `sender.CreateStream(...)` | la misma llamada, [un método de extensión](streaming.md) |

El namespace `MediatR.Pipeline` pasa a ser `Mediarion.Pipeline`, con los mismos tipos.

## Dónde se separa a propósito

`AutoRegisterRequestProcessors = true` registra tus pre y post procesadores en MediatR **y luego
no los llama nunca**, porque los behaviours que los ejecutan solo se agregan cuando nombras un
procesador uno a uno. Aquí la bandera hace lo que dice.

Eso se encontró escribiendo el sample de migración, no leyendo el código: el pre procesador
simplemente no corría. Nadie puede depender de una opción que no hace nada, y hacerla funcionar no
puede romper una migración que ya funcionaba.

## Dónde no puede seguirle

**Hacer un mock de `ISender.CreateStream`.** `CreateStream` es un método de `ISender` allá y un
método de extensión aquí, porque `ISender` vive en el paquete sin dependencias y el streaming
necesita `IAsyncEnumerable`. El sitio de llamada es idéntico; una biblioteca de mocking no puede
interceptar un método de extensión. Toma `IStreamSender` en el código bajo prueba y haz el mock de
eso. Ver [streaming](streaming.md).

**Una clave de licencia.** No hay ninguna que configurar, y nada lee una.

## Qué revisar después del cambio

1. Que compile. Si no, el error nombra un tipo, y la tabla de arriba dice cómo se llama ahora.
2. Que el orden de los behaviours sea el orden en que los registraste, de fuera hacia dentro — la
   misma regla que antes.
3. Si usabas `AutoRegisterRequestProcessors`, ahora tus procesadores corren. Eso es un cambio en lo
   que hace la aplicación, aunque tu código no haya cambiado.
4. Si publicas [ahead of time](aot.md), agrega `Mediarion.SourceGenerator`. El mediador en tiempo
   de ejecución cierra un genérico sobre un tipo que aprende mientras corre, cosa que una imagen
   nativa no puede hacer, y el tipo está marcado con `[RequiresDynamicCode]` para que te lo diga la
   compilación y no el primer request.
