# Security

## Reporting something

Use GitHub's private vulnerability reporting: **Security → Report a vulnerability** on
<https://github.com/Veneury/Mediarion>. That opens a thread only the maintainers can see, so
nothing is public while a fix is being worked out.

Please do not open a normal issue for a vulnerability. A public issue tells everyone at once,
including the people you would rather it did not.

What helps in a report: what an attacker can do, the configuration and input that gets them there,
and which version you saw it on. A failing test is worth more than a description.

You should get an acknowledgement within a week. If a week passes with nothing, assume the message
did not arrive and say so in a public issue without describing the problem.

## What counts

A mediator decides which code runs for a message, so the interesting cases are about reaching
code that was never meant to be reachable:

- A request that reaches a handler the registration never named, or a type that is constructed
  or invoked without being registered.
- Anything that lets one operation see values belonging to another. The mediator holds no state
  between operations on purpose, and a way to make it hold some is a vulnerability.
- A behaviour that is skipped. A pipeline is where authorisation and validation live, so a
  request that gets past one that was registered for it is the worst thing on this list.

Not vulnerabilities, though still worth an ordinary issue: a handler that is not found, a
registration mistake reported unhelpfully, or a slow pipeline.

## Supported versions

Before 1.0, only the most recent version is supported. A fix goes out as a new version rather than
being backported.

After 1.0 this section will say which majors receive fixes and for how long.

## What is already known

Nothing, yet. The library is new and nobody has used it in anger.

Two things are worth knowing about its shape. An exception thrown by a handler comes out as the
handler threw it, unwrapped — that is deliberate, so a `catch` in the application still works,
but it means a handler that leaks something in a message leaks it to whoever sees the exception.
And a handler that sends another request through the mediator will recurse as far as the stack
allows, because the mediator does not count depth: a cycle between handlers is a stack overflow
and not a caught error.
