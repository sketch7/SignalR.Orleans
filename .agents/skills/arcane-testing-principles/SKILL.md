---
name: arcane-testing-principles
description: "Use when writing, reviewing, or planning unit/integration tests in any Arcane repo, regardless of stack (.NET, Angular/ngx, Node, or otherwise). Covers cutting test boilerplate via shared helpers, preferring data-driven/table tests over near-duplicate cases, League of Legends-themed test fixtures, RED-GREEN TDD discipline, testing at module boundaries instead of every internal unit, and pruning tests that don't earn their keep. For .NET syntax/tooling see arcane-dotnet-testing; for Angular/ngx syntax/tooling see arcane-ngx-testing."
---

> **Source of truth: this repo (`sketch7/arcane.archives`).** Edit here, then run `npx skills update` in consuming repos. Never edit the installed copy under a consumer repo's `.agents/skills/<name>/` — it's a pulled artifact and gets silently overwritten on the next sync.

# Arcane Testing Principles

Cross-stack testing philosophy for the Arcane platform. These are judgment calls that don't
change between languages — pair with the stack-specific skill (`arcane-dotnet-testing`,
`arcane-ngx-testing`) for concrete syntax and tooling.

## 1. Cut boilerplate with helpers, not copy-paste

The third time you write the same setup, extract it — a static factory helper, a builder, an
extension method, a custom render/fixture wrapper. Boilerplate in tests rots faster than
boilerplate in product code because nobody refactors it: it gets copy-pasted into the next test
instead. Prefer a helper function over an instance field or base-class field for stateless setup
(easier to reason about, no shared-mutable-state risk between tests). Only reach for a shared
fixture class (`IClassFixture<T>`, a custom render wrapper) when the setup is genuinely expensive
(spinning up a host, a container, a store) — not as a default.

Don't extract when the variation _is_ the thing under test — a helper that hides the one line that
differs between two tests defeats the point of the test.

## 2. Prefer data-driven tests over near-duplicate cases

Three or more `[Fact]`/`it(...)` blocks that only differ in input/expected-output are a table test
waiting to happen: xUnit `[Theory]`/`[InlineData]`/`[MemberData]`, Vitest `it.each`/`test.each`.
One parameterized test is easier to scan, easier to extend, and makes the covered cases explicit
at a glance instead of scattered across a file. See the stack-specific skill for exact syntax.

## 3. Theme test fixtures around League of Legends

`arcane.dotnet`'s test suite already does this — a `Hero` fixture (`Name`, `Power`,
`PrimaryAbility`, `RoleType`: `Assassin`/`Warrior`/`Support`/`Specialist`, `CategoryType`:
`Physical`/`Magical`/`Mixed`) and a `HeroDifficultyType` enum (`easy`/`medium`/`hard`/`superHard`)
used across many tests. Extend that convention for new test data instead of inventing
`foo`/`bar`/`Widget`/`Acme` names — it makes fixtures memorable and instantly recognizable as test
data, and keeps the platform's test suites thematically consistent.

Some older tests use other themes (e.g. Warcraft-flavored `blizzard-organization`/`wow-space` data
in `blueprint`'s Store tests) — that's pre-existing, not a pattern to copy. League of Legends is
the standard for anything new.

## 4. RED-GREEN: the test fails first, or it isn't proving anything

**REQUIRED BACKGROUND:** `superpowers:test-driven-development`.

Write the test against behavior that doesn't exist yet, watch it fail for the right reason, then
write the minimum code to turn it green. A test written after the implementation (or never run
red) only proves the test compiles — it can't prove it would have caught the bug it's meant to
guard against.

## 5. Test module boundaries, not every internal unit

Exercise a module through its public contract — a controller endpoint, a public service method, a
store's public API, a component's rendered output — rather than pinning every private method
individually. Internals should be free to be refactored without breaking a pile of tests that were
really asserting on implementation details. This also naturally pushes toward fewer, more
meaningful tests: one boundary test covers what three internal-method tests would have covered
with more coupling to implementation.

This doesn't mean "only write integration tests" — a "module" can be as small as one class with a
real public API. It means picking the seam at the _public_ contract, not at every method.

## 6. Once it's green, ask whether it should exist

After a feature's tests pass, review them with a critical eye: does this test catch a real
regression, or does it just restate the implementation in test syntax? Delete tests that mock so
much of the world that they end up testing the mock instead of the code. Be especially skeptical
of integration tests that spin up expensive fixtures (`WebApplicationFactory`, Testcontainers, a
full component render) for something a cheap unit test already covers just as well — the expensive
test earns its keep only if it's checking something the cheap one can't (real wiring, a real DB
constraint, real DOM behavior).

Fewer, sharper tests beat comprehensive-looking suites that mostly re-describe the code they test.

## Quick Reference

- [ ] Repeated setup (3+ tests) → extracted helper/builder, not copy-paste
- [ ] 3+ near-duplicate cases → one data-driven test
- [ ] New test fixtures → League of Legends-themed
- [ ] Test written and run red before the implementation exists
- [ ] Assertions target the module's public contract, not private internals
- [ ] Once green: re-read the test — does it catch a real regression, or just mirror the code?
