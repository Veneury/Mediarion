# Releasing

A version on NuGet cannot be replaced, only delisted. Everything here exists because of that.

## Once, before the first release

There is no NuGet API key in this repository, and there should never be one. Publishing uses
trusted publishing: the job proves who it is with a token GitHub signs for this workflow, in this
repository, in this environment, and nuget.org hands back a key that lasts an hour.

1. On nuget.org, open *your username → Trusted Publishing* and add a policy:

   | Field | Value |
   |---|---|
   | Policy Name | anything, e.g. `Mediarion release` |
   | Package Owner | the nuget.org account that will own the packages |
   | CI/CD Provider | GitHub Actions |
   | Repository Owner | `Veneury` |
   | Repository | `Mediarion` |
   | Workflow File | `release.yml` — the file name only, no `.github/workflows/` |
   | Environment | `nuget` — must match `environment:` in the workflow |
   | Scopes | Push, *Push new packages and package versions*, pattern `Mediarion*` |

   The workflow file name and the environment are part of what nuget.org checks, so renaming
   either one stops publishing until the policy is updated to match. That is the point of them.

2. Add the nuget.org **profile name** as the repository secret `NUGET_USER`, under
   *Settings → Secrets and variables → Actions*. Today that value is `vdevers`. It is the
   nuget.org account name, not the email address and not the GitHub one, and where the two
   differ it is the account that **created** the policy rather than the one that owns the
   packages — nuget.org says so in the error when it is wrong. It is not sensitive, but keeping
   it out of a public file is what NuGet recommends, and the workflow stops with a clear message
   when it is missing.

3. Open *Settings → Environments → nuget* and add yourself as a required reviewer.

   Be clear about what that gate is. The whole job runs inside the environment, so GitHub holds
   it **before it starts**: approving means "start this release", not "these packages look
   right". Nothing is built, tested or packed until you approve, and once you do, the job runs
   to the end and pushes without stopping again.

   That is still the thing worth having — a tag pushed by accident does not publish anything on
   its own — but if what you want is to see the packages and then decide, the job has to be split
   in two, with only the pushing half in the environment.

   The environment is created by the first run if it does not exist, but without a reviewer it
   does not stop anything.

On a private repository a new policy is only provisionally active for seven days and lapses if
nothing is published in that time. This repository is public, so that does not apply, but it is
worth knowing if the repository is ever made private.

## Each release

1. Make sure `main` is green and `CHANGELOG.md` describes the version under a heading of its own
   rather than `Unreleased`.
2. Tag the commit and push the tag:

   ```
   git tag v0.1.0
   git push origin v0.1.0
   ```

3. The `Release` workflow waits for your approval before doing anything, because the job runs
   inside the `nuget` environment. Approve it from the run page: **Review deployments →
   nuget → Approve and deploy**.
4. It then builds, runs the whole suite on the tagged commit, runs the migration sample against
   MediatR, publishes the ahead-of-time sample natively and runs it, packs the three packages,
   pushes them to NuGet and creates a GitHub release with them attached. It does not stop
   again.

The version comes from the tag, so nothing needs editing to release. `VersionPrefix` in
`Directory.Build.props` only names the builds made in between.

## If it goes wrong

- **A release failed before pushing anything.** Nothing went to NuGet, so nothing is stuck. Fix the
  cause, delete the tag that failed, and release the next version number rather than moving the tag
  onto the fix: a tag that has already been fetched somewhere should not change what it points at.
- **A package failed to push and others went up.** Re-run the job. The push uses
  `--skip-duplicate`, so what is already on NuGet is left alone. The key is asked for again on the
  re-run, since each one lasts only an hour.
- **The token exchange fails with HTTP 401**, saying no matching trust policy was found for the
  user. `NUGET_USER` does not name the nuget.org account that created the policy. This is how
  0.1.0 failed the first time: the secret held the wrong name, everything else passed, and the
  job died one step before pushing. Fix the secret and use **Re-run failed jobs** on the same
  run — the tag does not need to move, and the NuGet key is asked for again because each one
  lasts an hour.
- **The push is rejected as unauthorized.** The policy on nuget.org no longer matches the job: check
  the workflow file name, the environment name and the package owner against the table above.
- **A version went out that should not have.** It cannot be taken back. Delist it on nuget.org so
  it stops appearing in search and in the version list, then release the fix as a new version.
  Delisting does not break anyone who already depends on that exact version, which is the point.
- **The tag was wrong.** Deleting a tag does not unpublish anything. If the workflow had already
  pushed, treat it as the case above.

## Signing

Two different things go by that name, and only one of them is done here.

**Strong naming is automatic.** Every assembly is signed with `mediarion.snk`, which is in the
repository. That is deliberate: a strong name is an identity, not a security boundary, since anyone
can strip one and re-sign with a key of their own. Keeping the private half secret would buy
nothing and would cost delay signing and verification skipping for everyone who builds. It is there
because a strong-named assembly on .NET Framework can only reference other strong-named assemblies,
so without it a signed codebase on net472 cannot use Mediarion at all.

Changing that key changes the identity of every assembly and breaks everyone who references them.
It should not be changed.

**Author signing the packages is not done, and needs money rather than work.** nuget.org requires a
code signing certificate that chains to a root trusted by Windows; it rejects self-issued ones.
Those now come with a hardware token or a cloud HSM, so there is no way to add this without buying
one and holding it somewhere the release job can reach.

What exists without it: nuget.org repository-signs every package it accepts, automatically, so the
published packages already carry a signature proving they have not been altered since upload. What
is missing is the author signature, which would additionally prove they came from whoever holds the
certificate.

If a certificate is bought later, the work is a `dotnet nuget sign` step between packing and
pushing, and registering the certificate on the nuget.org account beforehand.

## Version numbers

Semantic versioning, with the caveat already in `CHANGELOG.md`: before 1.0 a minor version may
break things. Pre-release versions are tagged the same way and are published as pre-release,
which the workflow works out from the `-` in the tag.
