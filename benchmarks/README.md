# Benchmarks

## How to read this

Each number is how many times slower than the same handler called directly through its
interface, measured in the same run with BenchmarkDotNet. The handler does nothing, on purpose:
what is being measured is what a library adds around it. Absolute times move between machines;
the multiple is what travels.

```
dotnet run --project benchmarks/Mediarion.Benchmarks -c Release -- --filter "*SendBenchmarks*"
```

## One request, five ways

Three short runs on a laptop, because one run of a benchmark with an eighteen-nanosecond floor
says less than it looks like it says.

| | Run 1 | Run 2 | Run 3 | Allocated |
|---|---|---|---|---|
| Called directly | 1.00x | 1.00x | 1.00x | 120 B |
| **Mediarion, generated** | 3.33x | 2.78x | 3.83x | 272 B |
| MediatR 12.5.0 | 4.55x | 3.47x | 5.35x | 312 B |
| **Mediarion, run time** | 11.68x | 9.64x | 13.23x | 752 B |
| Mediator 3.1.0-rc.1 | 1.11x | 0.83x | 1.30x | 48 B |

The ratios swing by a third between runs and the order never does, which is the part worth
believing.

## What it says

**The generated path beats MediatR**, by about a fifth, in every run. That is the path an
application published ahead of time has to use anyway, so the thing that makes the library work
under AOT also makes it faster than what it replaces.

**The run-time path is about two and a half times slower than MediatR**, which is not a design
tax — both engines answer the same question the same way — and 752 bytes a call against
MediatR's 312 says where to look. This is a defect, not a property, and it is written here
rather than left out because a benchmark that only reports the flattering number is worth
nothing.

**Mediator runs at the speed of calling the handler yourself**, and allocates a third of what a
direct call does. Two reasons, and only one of them is effort: it generates its dispatch, which
this library also does, and its handlers return `ValueTask`, which does not allocate for a
result that is already there. `Task` is what MediatR returns, and matching MediatR is the whole
reason this library exists, so that difference is not one to close.

If speed is what you are choosing on, and you are on .NET 6 or later, and you are willing to
write your handlers against a different set of interfaces, [Mediator] is the better answer and
this table is how you can tell.

[Mediator]: https://github.com/martinothamar/Mediator

## No budget in CI, yet

Mapperion, the sister project, holds its benchmark ratios in CI and fails a build that regresses
one. That is not done here on purpose: the floor is eighteen nanoseconds, the ratios move by a
third between runs of untouched code, and the same shape of check produced three false positives
there on exactly this kind of scenario. A guard that cries wolf gets ignored.

What would make it worth adding is a scenario with a floor big enough to measure against — a
pipeline of several behaviours, or a notification with a handful of handlers — and that is the
next thing to build here.
