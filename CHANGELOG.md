# Changelog

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioned according to [SemVer 2.0](https://semver.org/).

Before v1.0, a minor version may introduce breaking changes.

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
