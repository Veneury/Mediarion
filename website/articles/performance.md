# Performance

## How to read this

Every entrant is measured in the same run with BenchmarkDotNet, on the same machine, against the
same reference. Absolute times move between machines; what one entrant is as a multiple of
another **in the same run** is what travels. Allocation per operation travels too — it is decided
by the code and not by the runner.

Numbers below come from a laptop and are shown as several runs rather than one, because a single
run of a benchmark with a small floor says less than it looks like it says.

## A request through four behaviours

Four behaviours is what an application that uses a mediator seriously tends to have: logging,
validation, a transaction, a metric.

| | Run 1 | Run 2 | Run 3 | Allocated |
|---|---|---|---|---|
| Written out by hand | 0.91 ns | 0.80 ns | 0.86 ns | 0 B |
| **Mediarion, generated** | 221 ns | 223 ns | 200 ns | 688 B |
| **Mediarion, run time** | 288 ns | 221 ns | 226 ns | 688 B |
| MediatR 12.5.0 | 346 ns | 338 ns | 329 ns | 864 B |

The hand-written row is four private calls passing a request down to a handler — what the code
would be if nobody had reached for a mediator. The JIT flattens it into one call ending at a
cached task, so it measures under a nanosecond. That is the honest floor, and it is also why it
is useless as a divisor: the multiple over it is four hundred, and read 415x and 268x on two runs
of identical code.

Against MediatR, which does the same work through a pipeline it builds the same way, Mediarion's
run-time path measured 0.792, 0.839, 0.833, 0.654 and 0.688 across five runs, and the generated
path 0.696, 0.662, 0.640, 0.659 and 0.609. **Both are faster, the generated one by about a
third**, and both allocate 688 bytes against 864.

## A request with no behaviours

Here the multiple is over the same handler called directly through its interface. This is the
cheapest possible request, and the least interesting: the floor is seventeen nanoseconds and
almost all of what is being measured is the library's fixed cost.

| | Run 1 | Run 2 | Allocated |
|---|---|---|---|
| Called directly | 1.00x | 1.00x | 120 B |
| Mediator 3.1.0-rc.1 | 1.17x | 1.05x | 48 B |
| **Mediarion, generated** | 2.85x | 2.85x | 176 B |
| Mediarion, pipeline alone | 2.80x | 2.85x | 176 B |
| **Mediarion, run time** | 4.29x | 3.64x | 176 B |
| MediatR 12.5.0 | 4.97x | 4.39x | 312 B |

The generated path and the pipeline on its own measure the same, which is the point of having
both rows: the generated dispatch costs nothing measurable over calling the pipeline directly.
What separates the run-time path from them is the dictionary and the wrapper it goes through to
work out which pipeline to call — about fifteen nanoseconds.

## Where this library loses

[Mediator](https://github.com/martinothamar/Mediator) runs at the speed of calling the handler
yourself, and allocates a third of what a direct call does. Two reasons, and only one is effort:
it generates its dispatch, which this library also does, and its handlers return `ValueTask`,
which does not allocate for a result that is already there.

`Task` is what MediatR returns, and matching MediatR is the whole reason this library exists, so
that difference is not one to close. If speed is what you are choosing on, and you are on .NET 6
or later, and you are willing to write your handlers against a different set of interfaces,
Mediator is the better answer and the table above is how you can tell.

## What these numbers used to be

The first time this was measured the run-time path was **12.5x and 752 bytes** — two and a half
times slower than MediatR rather than faster than it. Splitting the row in two found it: the
pipeline on its own measured the same as the whole path, so the cost was not the dispatch.

It was the registration. `AddMediarion` registered the two behaviours that run pre- and
post-processors whether or not any processor existed, on the grounds that each is an empty loop
when there is nothing to run. That was true, and it cost 110 nanoseconds and 480 bytes on every
request, because an open-generic registration is closed by the container per pair and each of
those behaviours asks the container for an enumerable of its own.

They are registered now only when the configuration knows there are processors — which is what
the generated registration always did, so the fix also removed a disagreement between the two.

## The budget in CI

`benchmarks/baseline.json` records what the four-behaviour scenario is allowed to cost, and CI
fails a build that goes over. The empty-pipeline scenario is deliberately not held: its ratios
move by a third between runs of untouched code, and a guard that cries wolf gets ignored.

Two numbers per entrant, held differently on purpose.

The **time** is a multiple of MediatR in the same run, and the recorded figure is the worst of
five runs plus fifteen per cent. MediatR is the reference rather than the hand-written version
because it does the same work on the same machine and moves with the runner, and because 12.5.0
is frozen under Apache-2.0, so the reference cannot shift underneath the budget. This half is the
loose half and is there to catch something large.

The **bytes** are an exact ceiling with no tolerance at all, because allocation per request does
not vary between machines. This is the sharp half, and it is the half that would have caught the
one performance regression this library has actually had: the 480 bytes above survived three
releases with the whole test suite green.

```
dotnet run --project benchmarks/Mediarion.Benchmarks -c Release -- --filter "*PipelineBenchmarks*"
```

A change that is meant to cost something raises the number in `baseline.json` in the same pull
request, so the cost is agreed rather than discovered.
