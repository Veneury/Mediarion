# Contributing

## The public surface is written down

`PublicAPI.Shipped.txt` beside each shipping project lists everything that project exposes, and
the build checks the two against each other. Adding, removing or changing anything public fails the
build until the file is updated, which means it shows up in the diff and gets reviewed like the
rest of the change.

- **Adding something public**: the build fails with `RS0016` and the message contains the exact
  line to add. Put it in `PublicAPI.Unshipped.txt`.
- **Removing or changing something public**: the build fails with `RS0017` for the line that no
  longer matches. If the version it was shipped in is already on NuGet, this is a breaking change:
  say so in `CHANGELOG.md`. Before 1.0 that is allowed; after it, it is not.
- **Releasing**: move everything from `PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt` and
  leave the unshipped file with only its `#nullable enable` line.

An IDE offers "Add to public API" as a fix on `RS0016`, which does the first case for you.

`Mediarion.SourceGenerator` is not tracked this way. Nothing in its assembly is meant to be
called: what it exposes to a user is the code it writes, and that is covered by the tests that
send the same requests through both engines.

## Before opening a pull request

- `dotnet test Mediarion.sln -c Release` passes, on .NET 8, 9 and 10. The packages also target
  .NET Framework 4.7.2 and .NET Standard, and those are built but not run: a suite on Framework
  is worth adding and is not there yet.
- The migration sample passes. It fails when this library and MediatR 12.5.0 stop agreeing, and
  when the two versions of the layer differ by anything other than the name of the library.
- The ahead-of-time sample publishes natively and runs. Anything that needs reflection or code
  emitted at run time breaks it, which is the point of it.
- Warnings are errors here, including the analyzers and the missing-documentation one. A public
  member without XML documentation does not build.
- Comments in code are XML documentation and nothing else.

## Copyright

You keep the copyright in what you write. There is no contributor licence agreement and nothing to
sign: opening a pull request licenses your contribution under the same MIT terms as the rest.

That is deliberate, and [GOVERNANCE.md](GOVERNANCE.md) explains why it is the thing that makes the
licence promise worth anything.

## Releasing

See [RELEASING.md](RELEASING.md).
