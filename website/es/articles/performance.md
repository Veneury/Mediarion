# Rendimiento

## Cómo leer esto

Cada participante se mide en la misma corrida con BenchmarkDotNet, en la misma máquina, contra la
misma referencia. Los tiempos absolutos cambian entre máquinas; lo que un participante es como
múltiplo de otro **en la misma corrida** es lo que viaja. La memoria reservada por operación
también viaja: la decide el código y no la máquina.

Los números de abajo salen de un portátil y se muestran como varias corridas en lugar de una,
porque una sola corrida de un benchmark con un suelo pequeño dice menos de lo que parece.

## Un request a través de cuatro behaviours

Cuatro behaviours es lo que suele tener una aplicación que usa un mediador en serio: logging,
validación, una transacción, una métrica.

| | Corrida 1 | Corrida 2 | Corrida 3 | Reservado |
|---|---|---|---|---|
| Escrito a mano | 0.26 ns | 0.54 ns | 0.86 ns | 0 B |
| Mediator 3.1.0-rc.1 | 22 ns | 24 ns | 24 ns | 0 B |
| **Mediarion, generado** | 166 ns | 152 ns | 185 ns | 688 B |
| **Mediarion, en tiempo de ejecución** | 174 ns | 184 ns | 161 ns | 688 B |
| MediatR 12.5.0 | 256 ns | 229 ns | 246 ns | 864 B |

La fila escrita a mano son cuatro llamadas privadas que pasan un request hacia un handler — lo que
sería el código si nadie hubiera echado mano de un mediador. El JIT lo aplana en una sola llamada
que termina en una tarea cacheada, así que mide menos de un nanosegundo. Ese es el suelo honesto, y
también es la razón por la que no sirve como divisor: el múltiplo sobre él es de cuatrocientos, y
marcó 415x y 268x en dos corridas del mismo código.

Contra MediatR, que hace el mismo trabajo a través de un pipeline que construye igual, el camino
en tiempo de ejecución de Mediarion midió 0.792, 0.839, 0.833, 0.654 y 0.688 en cinco corridas, y
el camino generado 0.696, 0.662, 0.640, 0.659 y 0.609. **Los dos son más rápidos, el generado como
en un tercio**, y los dos reservan 688 bytes contra 864.

Mediator está en esta tabla porque la otra planteaba una pregunta que no podía responder. Allí gana
con el pipeline vacío, donde toda la medición es el coste fijo del despacho, y lo que uno supondría
es que cuatro behaviours cierran la distancia. **La distancia se abre.** Mediator pasa esos mismos
cuatro behaviours en unos 22 nanosegundos y no reserva nada, contra 170 y 688 bytes aquí — siete u
ocho veces más rápido en lugar de dos y media. Genera el pipeline entero al compilar y usa
`ValueTask` de principio a fin, así que no hay cadena de delegados que construir ni nada que
encajar en un objeto.

## Un request sin behaviours

Aquí el múltiplo es sobre el mismo handler llamado directamente por su interfaz. Este es el request
más barato posible, y el menos interesante: el suelo son diecisiete nanosegundos y casi todo lo
que se mide es el coste fijo de la biblioteca.

| | Corrida 1 | Corrida 2 | Reservado |
|---|---|---|---|
| Llamada directa | 1.00x | 1.00x | 120 B |
| Mediator 3.1.0-rc.1 | 1.17x | 1.05x | 48 B |
| **Mediarion, generado** | 2.85x | 2.85x | 176 B |
| Mediarion, solo el pipeline | 2.80x | 2.85x | 176 B |
| **Mediarion, en tiempo de ejecución** | 4.29x | 3.64x | 176 B |
| MediatR 12.5.0 | 4.97x | 4.39x | 312 B |

El camino generado y el pipeline por su cuenta miden lo mismo, que es el motivo de tener las dos
filas: el despacho generado no cuesta nada medible por encima de llamar al pipeline directamente.
Lo que separa al camino en tiempo de ejecución de ellos es el diccionario y el envoltorio por los
que pasa para averiguar qué pipeline llamar — unos quince nanosegundos.

## Dónde pierde esta biblioteca

[Mediator](https://github.com/martinothamar/Mediator) corre a la velocidad de llamar al handler tú
mismo, y reserva un tercio de lo que reserva una llamada directa. Dos razones, y solo una es
esfuerzo: genera su despacho, cosa que esta biblioteca también hace, y sus handlers devuelven
`ValueTask`, que no reserva para un resultado que ya está ahí.

`Task` es lo que devuelve MediatR, y parecerse a MediatR es la razón entera por la que existe esta
biblioteca, así que esa diferencia no es una que se vaya a cerrar. Si lo que decide es la
velocidad, y estás en .NET 6 o posterior, y estás dispuesto a escribir tus handlers contra otro
conjunto de interfaces, Mediator es la mejor respuesta y la tabla de arriba es cómo puedes saberlo.

## Qué eran antes estos números

La primera vez que se midió, el camino en tiempo de ejecución era de **12.5x y 752 bytes** — dos
veces y media más lento que MediatR en lugar de más rápido. Partir la fila en dos lo encontró: el
pipeline por su cuenta medía lo mismo que el camino completo, así que el coste no era el despacho.

Era el registro. `AddMediarion` registraba los dos behaviours que ejecutan los pre y post
procesadores hubiera o no algún procesador, con el argumento de que cada uno es un bucle vacío
cuando no hay nada que ejecutar. Era cierto, y costaba 110 nanosegundos y 480 bytes en cada
request, porque un registro genérico abierto lo cierra el contenedor por cada par y cada uno de
esos behaviours le pide al contenedor un enumerable propio.

Ahora se registran solo cuando la configuración sabe que hay procesadores — que es lo que el
registro generado hizo siempre, así que la corrección también quitó una discrepancia entre los
dos.

## El presupuesto en CI

`benchmarks/baseline.json` registra lo que el escenario de cuatro behaviours tiene permitido
costar, y CI falla una compilación que se pase. El escenario del pipeline vacío no se vigila a
propósito: sus proporciones se mueven un tercio entre corridas de código intacto, y un guardia que
grita "que viene el lobo" acaba ignorado.

Dos números por participante, vigilados de forma distinta a propósito.

El **tiempo** es un múltiplo de MediatR en la misma corrida, y la cifra registrada es la peor de
cinco corridas más un quince por ciento. MediatR es la referencia y no la versión escrita a mano
porque hace el mismo trabajo en la misma máquina y se mueve con el runner, y porque la 12.5.0 está
congelada bajo Apache-2.0, así que la referencia no puede cambiar por debajo del presupuesto. Esta
mitad es la mitad floja y está para atrapar algo grande.

Los **bytes** son un techo exacto sin ninguna tolerancia, porque la memoria reservada por request
no varía entre máquinas. Esta es la mitad afilada, y es la que habría atrapado la única regresión
de rendimiento que esta biblioteca ha tenido de verdad: los 480 bytes de arriba sobrevivieron tres
versiones con toda la suite de pruebas en verde.

```
dotnet run --project benchmarks/Mediarion.Benchmarks -c Release -- --filter "*PipelineBenchmarks*"
```

Un cambio que deba costar algo sube el número en `baseline.json` en el mismo pull request, para que
el coste se acuerde en lugar de descubrirse.
