# Governance

This project exists because a library people depended on changed its licence. So the first thing
this document has to answer is why that cannot happen here, and it has to answer it with something
better than a promise.

## The licence

Mediarion is MIT, and every version already published stays MIT. An MIT grant cannot be withdrawn
from a version that has been released: anyone who has it keeps the right to use it, forever, for
anything. Nothing anyone does later takes that away.

The question that actually matters is whether *future* versions could be relicensed. Here is the
mechanism, stated plainly:

- **There is no contributor licence agreement and no copyright assignment.** Contributors keep the
  copyright in what they write. It is licensed to the project under MIT, the same as to everyone
  else.
- A project whose copyright sits with a single party can relicense its future versions whenever
  that party likes. That is not a loophole, it is how copyright works, and it is the usual route by
  which an open source library becomes a commercial one.
- Because the copyright here is spread across whoever contributes, relicensing would need every one
  of them to agree. The more people contribute, the less possible it becomes.

**Be clear about the limit of that today.** There is currently one contributor of any size, so the
protection is mostly theoretical right now and gets real as others arrive. What is not theoretical
is the paragraph above about released versions: those are already beyond reach.

If you want a stronger guarantee than this, the honest advice is to pin a version and vendor it.
That advice applies to every dependency you have, not only this one.

## Who decides

One maintainer, today. Pretending otherwise would be theatre. Decisions are made in pull requests
and issues, in the open, and anyone can argue with them there.

The design criterion that settles most arguments is written down and does not change on a whim:
**a project migrating from MediatR should not have to work for it.** Where MediatR's shape is
reasonable, Mediarion copies it. Where it is not, Mediarion differs on purpose and writes down
why. A change that makes migration harder needs a better reason than taste.

There is one place it differs so far, and it is written down in the readme:
`AutoRegisterRequestProcessors` does what it says here and does nothing there.

The second criterion is that nothing configurable may silently do nothing. An option that is
accepted and then ignored is worse than an option that does not exist, and several have been found
and fixed here.

## Scope

An in-process mediator: requests to one handler, notifications to many, a pipeline around them,
and the pieces that make it usable — dependency injection and a source generator for trimmed and
ahead-of-time applications. Anything out of process is out: this is not a message bus and will
not grow into one.

## Breaking changes

Semantic versioning, with one exception already noted in `CHANGELOG.md`: before 1.0, a minor
version may break things. After 1.0 it may not.

The public surface is written down in `PublicAPI.Shipped.txt` and checked on every build, so a
breaking change cannot happen by accident. It has to be written into that file by someone, in a
pull request, where it can be seen and argued about.

## If this stops being maintained

Libraries get abandoned. The useful thing is to say in advance what that will look like:

- If there has been no release and no substantive response to issues for **six months**, that is
  what "unmaintained" means here, and the README should be edited to say so.
- The MIT licence means anyone can fork at any point, for any reason, without asking. That is the
  real backstop and it needs no cooperation from anyone.
- If maintenance is handed over, it will be announced in the repository and in the release notes,
  and the new maintainer inherits this document, including the licence commitment.

## Security

See [SECURITY.md](SECURITY.md).

## Conduct

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
