---
name: arcane-git-workflow
description: "Use when about to commit, branch, open a PR, or set up a git worktree in any Arcane repo — deciding whether to push straight to main or branch first, which base to branch from (main vs. an existing unmerged branch for a cascading/dependent change), what to name the branch, and where a worktree should live plus how to keep it building against linked sibling repos. Covers the platform's observed branch-naming convention (feature/, occasional fix//chore//ci/), why a direct push to main can trigger a real publish/deploy, PR/merge/cleanup norms, and the `.worktrees/` symlink fix for sibling-relative dependency links. For commit message format itself see arcane-conventional-commit."
---

> **Source of truth: this repo (`sketch7/arcane.archives`).** Edit here, then run `npx skills update` in consuming repos. Never edit the installed copy under a consumer repo's `.agents/skills/<name>/` — it's a pulled artifact and gets silently overwritten on the next sync.

# Arcane Git Workflow

Branching and PR conventions for the Arcane platform, as observed across `hexgate`, `cosmowrench(.api)`,
`blueprint(.client)`, `arcane.dotnet`, `vault`, `foundry`, `gemstone`, `arcane.ngx`, and `schematics`.
Pair with `arcane-conventional-commit` for the commit-message format itself — this skill only covers
branch/PR mechanics, not what a commit message should say.

## 1. Never commit directly to `main` — branch first, by default

A push to `main` isn't inert here: per `arcane-ci-cd-workflows`, it's what triggers the real publish
chain in a package repo (`dotnet-publish.yml`/`node-publish.yml` → tag → GitHub Release → main
version bump) or fires the `workflow_run`-triggered app-release/deploy dispatch in a deployable app
repo. An accidental direct commit to `main` isn't just a style violation — it can kick off a
publish or deploy nobody asked for.

**No branch protection rule enforces this anywhere in the platform** (private repos on GitHub's free
tier don't have it enabled) — same status as `arcane-conventional-commit`'s format: a convention
judged by review, not blocked by tooling. That makes it your judgment call to get right, not a
safety net you can lean on.

Default to creating a branch and opening a PR for any change. Push directly to `main` only when:
- the user explicitly instructs it for that specific change (a one-line docs typo, an emergency
  fix they've asked to land immediately), or
- the task is genuinely ambiguous about whether it warrants a PR — in that case, ask, don't guess.

This mirrors the general rule for any hard-to-reverse, shared-state action: confirm before, don't
apologize after. One instruction to push directly to `main` for one change doesn't carry over to
the next change in the same session — re-apply the default each time.

## 2. Branch from the right base

**Independent change**: branch from `main` — every sampled repo treats it as the trunk everything
forks from and everything eventually merges back to.

```bash
git switch main && git pull
git switch -c feature/invoice-fee-grid
```

**Cascading/dependent change** — new work that needs code from a branch that hasn't merged yet:
branch from that branch, not from `main`. Branching from `main` in this situation either duplicates
the unmerged work or produces a tangled merge once both land:

```bash
git switch -c feature/invoice-fee-grid-followup feature/invoice-fee-grid
```

Open the PR against the parent branch (`feature/invoice-fee-grid`), not `main` — retarget it to
`main` once the parent merges. If the parent branch gets updated while the stacked branch is still
open, rebase/merge the stacked branch onto the new tip before continuing rather than letting it
drift silently out of date. `superpowers:using-git-worktrees` is the tool for keeping a stacked or
parallel branch physically isolated from your main working copy if the two need to be worked on at
the same time — see §2a below for the platform-specific wrinkle it introduces.

### 2a. Worktrees live in `.worktrees/` at the repo root — watch sibling-relative links

Create worktrees project-local, under `.worktrees/<branch>/` at the repo root (e.g. for
`arcane.ngx`, `sketch7.arcane.ngx/.worktrees/feature/ts-strict-rollout/`) — this is what
`superpowers:using-git-worktrees` and native `EnterWorktree`-style tools already default to, keeps
every worktree contained under the repo it belongs to, and stays out of the shared `s:\git\`
parent folder.

The wrinkle: several repos resolve their sibling framework/library dependency as a **relative path
computed from the repo root** — `ArcaneLibsLinkPath`/`ArcaneLinkPath` in .NET repos (see
`arcane-dotnet-sibling-linking`) and `link:../sketch7.arcane.ngx/...` overrides in ngx-consumer
`pnpm-workspace.yaml` (see `arcane-ngx-workspace-linking`). A worktree under `.worktrees/<branch>/`
sits one directory level deeper than the repo root that path was written for, so it resolves inside
the worktree's own tree instead of the real sibling checkout — the build either fails outright or
silently picks up nothing.

Fix it once per sibling repo, not per worktree: add a symlink directly under `.worktrees/` (a
sibling of the branch folders, not inside one) named after the sibling repo, pointing at the real
sibling checkout:

```bash
# from the repo root, one-time setup
ln -s ../../sketch7.arcane.dotnet .worktrees/sketch7.arcane.dotnet   # .NET repos
ln -s ../../sketch7.arcane.ngx    .worktrees/sketch7.arcane.ngx      # ngx consumers
```

Every worktree's existing `../sketch7.arcane.dotnet`-style relative link then resolves through that
symlink to the real sibling, unchanged, for every current and future branch — no per-worktree edits
to `Directory.Packages.props` or `pnpm-workspace.yaml`. Confirm `.worktrees/` is gitignored (the
worktree-creation skill already verifies this) so the symlink itself never gets committed. For ngx
consumers, each worktree still needs its own `pnpm install` — `node_modules` isn't shared between
worktrees even once the link path resolves correctly.

## 3. Branch naming: `feature/<kebab-case>` by default

`feature/<short-kebab-case-description>` is the overwhelming convention across every sampled repo —
`feature/dev-profiles`, `feature/backup-disaster-recovery`, `feature/cosmo-testing-issues`. A
minority of branches use `fix/`, `chore/`, `ci/`, or `feat/` (the last is a rarer variant of
`feature/`, not a separate convention) when the branch is unambiguously that narrow — `ci/e2e-manual-dispatch`,
`chore/pr-only-ci`. Default to `feature/` unless the change is genuinely and only a fix/chore/CI
tweak; don't invent a new prefix scheme (no `bugfix/`, `hotfix/`, `task/`, ticket-number-only
branches, etc. anywhere in the platform).

## 4. Commit messages

See `arcane-conventional-commit` for the format, type list, and scope conventions — don't duplicate
that content here. It applies the same way whether you're committing to a fresh branch, a stacked
branch, or (in the rare explicitly-instructed case) directly to `main`.

## 5. PR, merge, and cleanup

- The PR's base is `main` for an independent change, or the parent branch for a stacked one (§2) —
  never merge locally and push a merge commit yourself; let the PR do the merge.
- All three merge strategies are enabled repo-side (squash, merge commit, rebase) — there's no
  hard rule, but squash is the common choice in observed history (`arcane-conventional-commit`
  notes roughly half of all commits platform-wide carry GitHub's auto-appended `(#123)` suffix,
  which only squash-merge produces). Default to squash unless the PR's commit history is itself
  meaningful and worth preserving.
- `delete_branch_on_merge` is enabled repo-side — the remote branch is cleaned up automatically once
  a PR merges, no manual `git push origin --delete` needed. Locally, prune stale branches with
  `git fetch --prune` (or the `commit-commands:clean_gone` skill, if available) rather than
  collecting dead branches over time.

## Quick Reference Checklist

- [ ] Change lands on a branch + PR by default; a direct push to `main` only happens on explicit instruction or after asking (§1)
- [ ] New independent work branches from `main`; work that depends on an unmerged branch branches from that branch instead, with the PR based against it (§2)
- [ ] Worktrees go in `.worktrees/<branch>/` at the repo root; a one-time symlink under `.worktrees/` keeps sibling-repo relative links (`ArcaneLibsLinkPath`, ngx `link:` overrides) resolving to the real checkout (§2a)
- [ ] Branch name is `feature/<kebab-case>` unless the change is unambiguously a narrower `fix/`/`chore/`/`ci/` (§3)
- [ ] Commit messages follow `arcane-conventional-commit` (§4)
- [ ] PR is merged (not pushed as a manual merge commit); local stale branches pruned via `git fetch --prune`, not left to accumulate (§5)
