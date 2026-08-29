---
name: arcane-ci-cd-workflows
description: "Use when adding, editing, or debugging a .github/workflows/ci.yml or cd.yml/release.yml in any Arcane repo — deciding which reusable workflow track a repo belongs to (published package vs. deployed app), wiring workflow_call inputs/secrets, or tracing why a repo's CD didn't publish/deploy after a push to main."
---

> **Source of truth: this repo (`sketch7/arcane.archives`).** Edit here, then run `npx skills update` in consuming repos. Never edit the installed copy under a consumer repo's `.agents/skills/<name>/` — it's a pulled artifact and gets silently overwritten on the next sync.

# Arcane CI/CD Workflows

No Arcane repo hand-rolls its own build/test/publish/deploy logic — every `.github/workflows/*.yml`
is a thin `uses:` wrapper around **reusable workflows hosted in one of two repos**, chosen by what
the repo produces. Get the track wrong and CD silently does the wrong thing (or nothing).

## Two tracks

| Repo produces                                            | CI source                                         | CD source                               | Example repos                                                    |
| -------------------------------------------------------- | ------------------------------------------------- | --------------------------------------- | ---------------------------------------------------------------- |
| A published NuGet/npm **package**                        | `sketch7/.github` (`dotnet-ci.yml`/`node-ci.yml`) | `sketch7/.github`'s publish chain       | `arcane.dotnet`, `arcane.ngx`, `Sketch7.SignalR.Orleans`         |
| A deployed **app/service** (container image, no package) | `sketch7/.github` (same CI workflows)             | `arcane.hexgate`'s own release workflow | `vault`, `foundry`, `gemstone`, `blueprint`, `cosmowrench(.api)` |

**CI is always `sketch7/.github`, for both tracks** — `dotnet-ci.yml`/`node-ci.yml` just build+test,
they don't know or care whether the result gets packaged or containerized. It's **CD that forks**:
package repos publish to a registry via `sketch7/.github`; deployable app repos build a Docker image
and hand off to `arcane.hexgate` instead.

## Track 1: CI (every repo)

```yaml
jobs:
  ci:
    uses: sketch7/.github/.github/workflows/dotnet-ci.yml@dotnet-libs-v2
    with:
      private-nuget-env-prefix: ARCANE_NUGET
      project-path: ./src/management # only for repos with a non-root solution, e.g. blueprint
    secrets:
      nuget-auth-token: ${{ secrets.GH_PKGS_PAT }}
      private-nuget-username: ${{ secrets.GH_BOT_USER }}
```

Node repos use `node-ci.yml` the same way. Solution/project resolution auto-discovers from
`package.json#dotnetBuildSln` when `solution-file` is omitted — most repos don't set it explicitly.

## Track 2a: CD for a package repo

Chains through `sketch7/.github`'s `@release-v1` workflows via job `needs`/`if` on prior outputs:

```
node-publish.yml / dotnet-publish.yml  →  prepare-release.yml  →  create-release.yml  →  node-bump-main.yml
```

- `publish` resolves/bumps the version and publishes; only proceeds on `main`/`v*`/`workflow`
  branches or an explicit `workflow_dispatch` input.
- `prepare-release` (only on a `main` push) opens/updates the `release/v{baseVersion} → v{major}`
  PR.
- `release` (only for a **non-prerelease** publish) tags the version, force-moves the floating
  `v{major}` tag, publishes a GitHub Release.
- `bump-main` (only when `release.outputs.is-latest == 'true'`) bumps `main`'s minor version —
  gated so publishing an LTS/backport release (e.g. `v1.x` while `main` is on `v2`) doesn't
  spuriously bump `main`.

Real example (`arcane.ngx`'s `cd.yml`): `permissions` needs `id-token: write` (OIDC),
`contents: write` (tag push), `packages: write`, `pull-requests: write`.

## Track 2b: CD for a deployable app repo

**Not `sketch7/.github`** — a separate reusable workflow lives in `arcane.hexgate` itself:

```yaml
jobs:
  release:
    uses: sketch7/arcane.hexgate/.github/workflows/release-dotnet-app.yml@main
    with:
      image: ghcr.io/sketch7/arcane.vault
      app: vault
      skip-build: ${{ github.event_name == 'workflow_run' }} # CI already verified the commit
    secrets: inherit
```

(`release-dotnet-fe-app.yml` is the equivalent for frontend/SSR apps like `blueprint.client`/
`cosmowrench`.) This builds the image, pushes it to `ghcr.io/sketch7/<app>`, then fires a
`repository_dispatch` (`app-release`) at `arcane.hexgate`, which is what actually deploys — see
`arcane.hexgate`'s own `CLAUDE.md` ("CD pipeline") for what happens after the dispatch. Trigger this
off a `workflow_run` for `CI` (not a raw push) to avoid building/testing twice; `dry_run` skips the
`arcane.hexgate` dispatch for a build-only test.

## Debugging "CD didn't do anything"

- **Publish job didn't run**: check the `if:` condition — package-repo publish only fires on
  `main`/`v*`/`workflow` branches or an explicit `workflow_dispatch.inputs.publish`.
  App-repo release triggers off `workflow_run` completing successfully (or manual dispatch) — a
  failed CI run means it never fires.
- **Tag pinned to a workflow didn't move**: `sketch7/.github` tags (`dotnet-libs-v2`, `release-v1`,
  etc.) are floating — `update-tags.yml` there force-moves them on push to `main`. If a workflow
  behaves like an old version, the consumer's `@tag` is stale relative to what actually merged, or
  the tag-mover itself hasn't run yet.
- **Wrong track entirely**: an app repo referencing `sketch7/.github`'s publish chain (or vice
  versa) is a sign the repo was scaffolded from the wrong template — check the table above.

## Related

`arcane.hexgate`'s `CLAUDE.md` documents the receiving end of the app-release dispatch (`deploy.yml`,
`deploy/<stack>.prod.versions.json`); `sketch7.github`'s `CLAUDE.md` documents the full input/secret
contract for every `sketch7/.github` workflow.
