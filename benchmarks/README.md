# Benchmarks

## How to read this

Each number is how many times slower than the same handler called directly through its
interface, measured in the same run with BenchmarkDotNet. The handler does nothing, on purpose:
what is being measured is what a library adds around it. Absolute times move between machines;
the multiple is what travels.

```
dotnet run --project benchmarks/Mediarion.Benchmarks -c Release -- --filter "*SendBenchmarks*"
```

## One request, six ways

Two short runs on a laptop, because one run of a benchmark with a seventeen-nanosecond floor
says less than it looks like it says.

| | Run 1 | Run 2 | Allocated |
|---|---|---|---|
| Called directly | 1.00x | 1.00x | 120 B |
| Mediator 3.1.0-rc.1 | 1.17x | 1.05x | 48 B |
| **Mediarion, generated** | 2.85x | 2.85x | 176 B |
| Mediarion, pipeline alone | 2.80x | 2.85x | 176 B |
| **Mediarion, run time** | 4.29x | 3.64x | 176 B |
| MediatR 12.5.0 | 4.97x | 4.39x | 312 B |

## What it says

**Both paths beat MediatR**, and allocate about half of what it does. The generated one is
ahead by a little under half; the run-time one by less, and by how much depends on the run.

**The generated path and the pipeline on its own measure the same**, which is the point of
having both rows: the generated dispatch costs nothing measurable over calling the pipeline
directly. What separates the run-time path from them is the dictionary and the wrapper it goes
through to work out which pipeline to call, and that is about fifteen nanoseconds.

**Mediator still runs at the speed of calling the handler yourself**, and allocates a third of
what a direct call does. Two reasons, and only one is effort: it generates its dispatch, which
this library also does, and its handlers return `ValueTask`, which does not allocate for a
result that is already there. `Task` is what MediatR returns, and matching MediatR is the whole
reason this library exists, so that difference is not one to close.

If speed is what you are choosing on, and you are on .NET 6 or later, and you are willing to
write your handlers against a different set of interfaces, [Mediator] is still the better answer
and this table is how you can tell.

[Mediator]: https://github.com/martinothamar/Mediator

## What these numbers used to be

The first time this was measured the run-time path was **12.5x and 752 bytes** — two and a half
times slower than MediatR rather than faster than it. Splitting the row in two found it: the
pipeline on its own measured the same as the whole path, so the cost was not the dispatch.

It was the registration. `AddMediarion` registered the two behaviours that run pre- and
post-processors whether or not any processor existed, on the grounds that each is an empty loop
when there is nothing to run. That was true and it cost 110 nanoseconds and 480 bytes on every
request, because an open-generic registration is closed by the container per pair and each of
those behaviours asks the container for an enumerable of its own.

They are registered now only when the configuration knows there are processors — which is what
the generated registration always did, so the fix also removed a disagreement between the two.

## No budget in CI, yet

Mapperion, the sister project, holds its benchmark ratios in CI and fails a build that regresses
one. That is not done here on purpose: the floor is eighteen nanoseconds, the ratios move by a
third between runs of untouched code, and the same shape of check produced three false positives
there on exactly this kind of scenario. A guard that cries wolf gets ignored.

What would make it worth adding is a scenario with a floor big enough to measure against — a
pipeline of several behaviours, or a notification with a handful of handlers — and that is the
next thing to build here. It would also be a better benchmark: every row above has an empty
pipeline, which is the common case and not the interesting one.
