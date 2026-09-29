# Ahead-of-time y el generador

## El problema

El mediador en tiempo de ejecución averigua qué handler responde a un request mientras el programa
corre, lo que implica cerrar un genérico sobre un tipo que solo conoce entonces. Una aplicación
publicada ahead of time no puede hacer eso: no hay JIT que construya el tipo cerrado.

La biblioteca lo dice en lugar de dejar que te enteres en el primer request — `Mediator` lleva
`[RequiresDynamicCode]`, así que un proyecto con el analizador de AOT encendido recibe un aviso de
compilación en la llamada, no un fallo en producción.

## La respuesta

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

Eso es todo. El generador encuentra cada handler del proyecto y escribe dos cosas:

- **el despacho**, como un `switch` con un caso por cada tipo de request, para que no se cierre
  nada en tiempo de ejecución;
- **el registro**, nombrando cada handler, porque un escaneo también es reflexión.

`AddAppMediator` se genera a partir del nombre de la clase, así que un mediador llamado
`OrderMediator` recibe `AddOrderMediator`.

## Lo que no hace

**Los behaviours del pipeline quedan en tus manos.** Qué behaviours aplican y en qué orden es una
decisión, y no es algo que adivinar de lo que haya tirado por el proyecto. Regístralos cerrados,
que es como una aplicación publicada ahead of time tiene que hacerlo de todos modos:

```csharp
services.AddTransient<IPipelineBehavior<PlaceOrder, int>, Logged<PlaceOrder, int>>();
```

Los pre y post procesadores se registran cuando
`[GeneratedMediator(RegisterRequestProcessors = true)]` lo dice, que es el reflejo de
`AutoRegisterRequestProcessors` del lado en tiempo de ejecución.

## Tu código no cambia

Los handlers, los behaviours y los procesadores son el mismo código de las dos maneras, y el
pipeline en el que corren también. Ese pipeline está **compartido entre los dos motores a
propósito**: no contiene reflexión, y compartirlo es lo que impide que alguna vez discrepen sobre
el orden de los behaviours. Una suite de pruebas manda los mismos requests por los dos y exige las
mismas respuestas.

## Diagnósticos

El generador dice lo que no puede hacer, en la declaración, mientras el proyecto compila.

| | |
|---|---|
| `MDR0001` | La clase marcada con `[GeneratedMediator]` no es `partial`, así que el despacho no tiene dónde ir. **Error.** |
| `MDR0002` | Dos handlers responden a un request. Un request tiene un handler; usa una notificación para el otro. **Error.** |
| `MDR0003` | Un request no tiene handler en este proyecto, así que enviarlo fallará en tiempo de ejecución salvo que se registre uno desde otro sitio. **Aviso**, porque el handler puede vivir legítimamente en otro ensamblado. |
| `MDR0004` | Un handler genérico abierto. El despacho generado es un `switch` sobre tipos de request conocidos al compilar y un genérico cerrado no es uno de ellos, así que queda fuera. **Aviso**, porque [el escaneo en tiempo de ejecución sí puede cerrarlo](pipeline.md#handlers-genéricos-abiertos) y negarse a compilar rechazaría código que funciona. |

## Comprobado, no supuesto

`samples/Mediarion.Aot`, en el repositorio, se publica con `PublishAot=true` y verifica sus propias
respuestas: requests, notificaciones, un manejador de excepciones y un stream. CI lo publica de
forma nativa y lo ejecuta, con los analizadores de trimming y de AOT encendidos, así que un cambio
que rompa ahead-of-time rompe la compilación en lugar del despliegue de alguien.
