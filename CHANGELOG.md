# Changelog

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioned according to [SemVer 2.0](https://semver.org/).

Before v1.0, a minor version may introduce breaking changes.

## [Unreleased]

### Added

- **`MDR0004`: an open generic handler is a compile-time error.** It cannot be registered by any
  container — an open implementation is closed against an open service type by matching type
  parameters position for position, and a handler's do not line up, since the request argument of
  `IRequestHandler<Wrapped<T>, T>` is `Wrapped<T>` and not `T`. Both engines used to skip one in
  silence, and nothing anywhere said so.
- Refusing it at registration was tried and reverted, after asking the other library what it does
  with one: it registers without complaint and fails on the first send with a container message
  about a missing service. Refusing would stop an application starting that starts today, over a
  request that may never be sent. The compile-time error catches it at the declaration, which is
  earlier and more precise than either, and the send still names the request when the generator is
  not in use.

### Fixed

- **The readme was wrong, and shipped wrong with 0.3.0.** It said streaming, exception handlers
  and the source generator were "not yet" while all three were released. Several edits to it over
  the previous versions had silently not applied — a replace against text an earlier edit had
  already changed — and nothing checked. It is rewritten against what the library actually does,
  and now covers the pilot, ahead-of-time, streaming, the open generic limit and where to find the
  benchmarks.

- **Streaming, in a package of its own: `Mediarion.Streaming`.** `IStreamRequest<T>`,
  `IStreamRequestHandler<,>`, `IStreamPipelineBehavior<,>` and `CreateStream`, with the shapes
  the library this is a drop-in for uses. A streaming request hands back each value as its
  handler reaches it rather than one value once it has finished.
- It is separate for one reason: `IAsyncEnumerable<T>` is in the runtime from .NET Core 3.0
  onwards and comes from a package on `netstandard2.0` and .NET Framework. Putting it in the
  core would mean everyone carrying that package, including everyone who never streams. The core
  still has no dependencies.
- The generator writes the streaming dispatch too, but only for a project that has the package,
  so ahead-of-time applications can stream. The sample publishes native with no IL warnings and
  streams.
- The migration sample streams on both libraries from one piece of source, and the whole
  difference between the two layers is still the name of the library.

### Known difference

- `CreateStream` is a method on `ISender` over there and an extension method on it here, because
  `ISender` lives in the package with no dependencies. Call sites are identical. What is not is
  mocking `ISender.CreateStream`, which no mocking library can intercept: take `IStreamSender`
  in the code under test and mock that instead.

## [0.3.0] - 2026-09-28

Exception handling, and nothing else. A minor because it adds to the public surface and takes
nothing away: an application on 0.2.0 can move to this one without touching a line.

It is the last piece of MediatR's shape that was missing apart from streaming, and streaming is
a decision rather than a task — `IAsyncEnumerable` on `netstandard2.0` means a dependency, and
the core having none is a promise worth more than a feature nobody has asked for yet.

### Added

- **Exception handlers.** `IRequestExceptionHandler<TRequest, TResponse, TException>` deals with
  one kind of failure out of one kind of request and may answer in its place;
  `IRequestExceptionAction<TRequest, TException>` runs on a failure and lets it carry on
  throwing. Both have the one-argument-fewer forms that take any `Exception`, and the state
  object, and the behaviours that run them — the same shapes as the library this is a drop-in
  for, so a handler written against that one compiles here unedited.
- Choosing a handler by the type of the exception is the one thing here that would need
  reflection: the behaviour catches an `Exception` and has to find the handlers registered for
  whatever it turned out to be, which means closing a generic over a type learned at run time.
  The choosing is moved to registration instead, where the exception type is known — by the scan
  or by the generator — and each handler is registered behind an adapter that tests the type
  itself. The ahead-of-time sample exercises it and still publishes native with no IL warnings.
- The generated registration names them too, so the ahead-of-time path has them without a scan.

### Fixed

- A handler written for `Exception` itself is not wrapped in that adapter. The adapter resolves
  the same service type it is registered as, so it would have been handed itself and called
  itself until the stack ran out. Caught before it shipped, and pinned by a test on both paths.

### Changed

- The exception behaviours sit outermost, ahead of everything the application adds. Innermost
  reads better — an exception handler is for what the handler threw, not for what a behaviour
  decided — and the other library puts them outside, and the difference is visible: a behaviour
  that logs on the way out never runs when the handler throws there. The migration sample caught
  it, and matching is the point of the library.

## [0.2.0] - 2026-09-28

A minor rather than a patch, and for the reason the format says: something that used to run no
longer does. A pre- or post-processor registered straight into the container, without the
configuration being told about it, is not run any more. Ask for it the documented way and
nothing changes. The public API is untouched — `PublicAPI.Unshipped.txt` is empty for both
packages — which is why this is a minor and not a major, and before 1.0 a minor is allowed to do
this anyway.

What it is mostly about is the measuring. There were no benchmarks in 0.1.0, which meant the
readme could not say anything about speed and neither could anybody else. There are now, with
the library that beats this one in the table, and writing them found that the run-time path was
two and a half times slower than the library it replaces. It is now faster than it.

### Added

- `samples/Mediarion.Helpdesk`, an application that uses the library the way an application
  would rather than the way a test does: a container, an open-generic behaviour wrapping
  everything, a closed one that refuses a request before its handler, a pre-processor, a handler
  that publishes a notification two handlers take, and a request with nothing to give back. It
  keeps a journal of every step, fails when a line is out of place, and CI runs it. The expected
  journal was wrong on the first run — a pre-processor was assumed to run inside the behaviours
  rather than outside them — which is the sort of thing a sample catches and a unit test cannot.
- `benchmarks/Mediarion.Benchmarks`, with four entrants beside a hand-written baseline: this
  library both ways, MediatR 12.5.0 and Mediator 3.1.0-rc.1. The numbers are in
  `benchmarks/README.md`, including the two that do not flatter this library — the run-time path
  is two and a half times slower than MediatR, and Mediator runs at the speed of calling the
  handler yourself.
- Thirteen tests for the generator, over a harness that compiles a snippet in memory and then
  compiles what the generator wrote. A generator that emits something plausible and invalid is
  the failure worth catching, and a test that read the text alone would miss it.

### Changed

- **`AddMediarion` registers the two behaviours that run pre- and post-processors only when the
  configuration knows there are processors.** They used to go on always, on the grounds that
  each is an empty loop when there is nothing to run. That was true and it cost 110 nanoseconds
  and 480 bytes on every request, because an open-generic registration is closed by the
  container per pair and each of those behaviours asks for an enumerable of its own. It also
  disagreed with the generated registration, which never did this.

  What changes for you: a processor registered straight into the container, without telling the
  configuration about it, is no longer run. That is the contract MediatR has, and a test pins
  it. Ask for it through `AddRequestPreProcessor`, `AddRequestPostProcessor` or
  `AutoRegisterRequestProcessors` and nothing changes.

- The pipeline no longer copies the behaviours into a list the container already handed it as
  an array, and a request with no behaviours at all reaches its handler without a delegate and a
  closure being built to get there.

- Together: the run-time path went from 12.5x a hand-written call to about 4x, and from 752
  bytes a request to 176. It was two and a half times slower than MediatR and is now faster than
  it, allocating about half. The generated path went from 3.8x to 2.85x. The numbers, and what
  they still lose to, are in `benchmarks/README.md`.

### Fixed

- **`MDR0003` was declared and never reported.** A diagnostic that exists and cannot fire is the
  same sin as a setting that is accepted and ignored. It now warns for a request the project
  declares and nothing in the project answers — a warning, because the handler may come from an
  assembly the generator cannot see. It found the one case in this repository on its first
  build.
- **A generated mediator in a project with no notifications did not compile**, because an empty
  switch is not valid C#. Nothing had ever compiled that shape until the benchmark project did.

### Documentation

- `RELEASING.md` said the release workflow waits for approval before pushing. It waits before
  starting: the whole job runs inside the `nuget` environment, so GitHub holds it at step zero.
  Approving means "start this release", not "these packages look right".
- The value of `NUGET_USER` is named rather than described, because 0.1.0 failed on it the first
  time: everything passed and the token exchange returned a 401 one step before the push. It is
  the nuget.org account that created the trusted publishing policy. That failure and its
  recovery — fix the secret, re-run the failed job, leave the tag alone — are written down.

## [0.1.0] - 2026-09-27

The first release. An in-process mediator for CQRS, MIT licensed, shaped like MediatR so that
moving a codebase is a change of namespace — and with no licence key, because that is why it
exists. MediatR 12.5.0 was the last release under plain Apache-2.0; from 13.0.0 the package is
dual licensed and asks for a key while your program runs.

It is 0.1 and not 1.0 for the obvious reason: nobody has used it. The shape is copied from a
library that has been in production for a decade, which is worth something, and the comparison
below is worth something, and neither is the same as somebody's application depending on it.

### Added

- `IRequest<TResponse>` and `IRequest`, handled by `IRequestHandler<,>` and `IRequestHandler<>`.
  A request has one handler; a request with nothing to give back answers with `Unit`, because a
  pipeline generic over a response needs one to be generic over.
- `INotification` and `INotificationHandler<>`, with the publishing strategy as a choice:
  `ForeachAwaitPublisher` one after another, which is the default and stops at the first failure,
  or `TaskWhenAllPublisher` together, which runs them all and reports the first thing that broke.
- `IPipelineBehavior<,>`, running outermost first in the order they were registered. A behaviour
  that does not call the next step short-circuits everything below it, which is how a cache or a
  guard is written.
- `IRequestPreProcessor<>` and `IRequestPostProcessor<,>`, running outside the behaviours: a
  pre-processor before every one of them, and a post-processor on what the handler returned
  rather than on what a behaviour did to it afterwards.
- `ISender`, `IPublisher` and `IMediator`. Most code wants one of the first two, and taking one
  of them says which and makes a test double smaller.
- `AddMediarion` for `Microsoft.Extensions.DependencyInjection`, with the same configuration
  object shape: `RegisterServicesFromAssembly`, `AddBehavior`, `AddOpenBehavior`,
  `AddRequestPreProcessor`, `AddRequestPostProcessor`, `Lifetime`, `NotificationPublisher`.
- **A source generator.** `[GeneratedMediator]` on a partial class writes the dispatch as a
  switch — one case per request, settled while the project compiles — and the registration
  naming every handler, so neither finding the handler nor finding the handlers needs
  reflection. That is what makes the library work in an application published ahead of time.
- Six target frameworks, from `net472` to `net10.0`, and **no package dependencies** in the core.

### Proved rather than claimed

- `samples/Mediarion.Migration` configures one ordering layer twice over a shared domain, once on
  MediatR 12.5.0 and once here, drives both through the same script and fails when they stop
  agreeing step for step. It covers a request with a response, one without, a notification with
  two handlers, an open-generic behaviour, a closed one that short-circuits, a pre-processor, a
  post-processor and a request sent as `object`.
- The source of both layers is embedded in that sample and compared as text. Every line that
  differs has to be the original with the library renamed and nothing else, so **the whole diff
  is the `using` lines and the namespace**. CI fails if a fourth kind of difference appears.
- `samples/Mediarion.Aot` is published with `PublishAot=true`, with the trimming and AOT
  analysers on, and checks its own answers. CI publishes it natively and runs it.
- A test suite sends the same requests through both engines and requires the same answers,
  including the ones that should fail.

### Differs from MediatR on purpose

- **`AutoRegisterRequestProcessors` does what it says.** There, the flag registers your
  processors and nothing ever calls them, because the behaviours that run them are only added
  when a processor is named one at a time; the setting is dead. Here it works. Nobody can be
  relying on a setting that does nothing, and making it work cannot break a migration that
  already worked.
- Streaming requests and exception handlers are not here yet. Streaming would mean a dependency
  on `netstandard2.0` for `IAsyncEnumerable`, and the core having none is a promise worth more
  than a feature nobody has asked for yet.

### Known shape worth knowing

- An exception thrown by a handler comes out as the handler threw it, unwrapped, so a `catch` in
  the application still works.
- The mediator does not count recursion depth. A handler that sends a request that reaches it
  again is a stack overflow rather than a caught error.
