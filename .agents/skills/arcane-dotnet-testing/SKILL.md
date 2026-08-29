---
name: arcane-dotnet-testing
description: "Use when writing or reviewing .NET unit/integration tests in an Arcane repo (arcane.dotnet, hexgate, vault, foundry, gemstone, blueprint, cosmowrench.api). Covers xUnit + Shouldly conventions, the FluentAssertions-to-Shouldly migration status per repo, Subject_WhenCondition_ExpectedOutcome naming, Theory/InlineData data-driven tests, boilerplate-avoiding test helpers, async Task rules, and test-double placement. For framework-agnostic judgment (module boundaries, pruning tests, League of Legends theming) see arcane-testing-principles; for WebApplicationFactory/Testcontainers integration-test host setup and controller/CQRS conventions see arcane-dotnet-aspnet-conventions."
---

> **Source of truth: this repo (`sketch7/arcane.archives`).** Edit here, then run `npx skills update` in consuming repos. Never edit the installed copy under a consumer repo's `.agents/skills/<name>/` — it's a pulled artifact and gets silently overwritten on the next sync.

# Arcane .NET Testing

Unit- and integration-test conventions for Arcane's .NET services, as actually practiced in
`arcane.dotnet` and `hexgate` (the two repos fully on the target convention) — read with
`arcane-testing-principles` (cross-stack judgment) and `arcane-dotnet-aspnet-conventions` §5 (the
`WebApplicationFactory`/`DbTestFactory` host-setup code for integration tests — not repeated here).

## 0. Assertion library: Shouldly is the target — check where your repo actually is

The platform is mid-migration off FluentAssertions (went non-OSS) onto **Shouldly** (BSD-3), driven
by `arcane.dotnet/docs/testing-refactor-plan.md`. Don't assume every repo has finished:

| Repo                           | Status                                                                                                                                                                                                  |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `arcane.dotnet`                | Target: xUnit + **Shouldly** + Moq only. Some older tests still use `Assert.*`/FluentAssertions — migrating them is in scope, don't add more.                                                           |
| `hexgate`                      | Already fully xUnit + Shouldly, no FA/Moq.                                                                                                                                                              |
| `vault`, `foundry`, `gemstone` | No FA/Moq found in sampled tests — consistent with Shouldly, but these repos have no written testing-instructions file; AGENTS.md is the only documented source.                                        |
| `blueprint`                    | Split: `*.Management.Store.Test` (unit layer) still **FluentAssertions + Moq**; `*.Management.Server.Tests` (integration layer, `WebApplicationFactory` + `Testcontainers.MsSql`) already **Shouldly**. |
| `cosmowrench.api`              | Store-layer unit tests still **FluentAssertions**.                                                                                                                                                      |

**Rule:** in a repo that's already on Shouldly, never introduce `FluentAssertions`/new `Assert.*`
calls. In a repo still on FluentAssertions (blueprint's Store layer, cosmowrench.api's Store
layer), match the existing file's library rather than mixing two assertion libraries in one test
class — raise migrating the whole project as a separate concern, don't do it inline with an
unrelated change.

Substitution cheat-sheet when migrating or writing fresh Shouldly code:

| FluentAssertions / xUnit Assert         | Shouldly                                                                                                  |
| --------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| `result.Should().Be(x)`                 | `result.ShouldBe(x)`                                                                                      |
| `Assert.Equal(expected, actual)`        | `actual.ShouldBe(expected)` (⚠️ argument order flips)                                                     |
| `action.Should().Throw<T>()`            | `Should.Throw<T>(() => action())`                                                                         |
| async throw                             | `await Should.ThrowAsync<T>(...)`                                                                         |
| `result.Should().BeOfType<T>()`         | `result.ShouldBeOfType<T>()`                                                                              |
| `collection.Should().Contain(x => ...)` | `collection.ShouldContain(predicate)`                                                                     |
| `x.Should().BeEquivalentTo(y)`          | `x.ShouldBeEquivalentTo(y)` (structural; no FA member-exclusion options — verify nothing relied on those) |

## 1. Naming: `Subject_WhenCondition_ExpectedOutcome`

```csharp
[Fact]
public void Middleware_Returns400_WhenTenantNotResolved() { ... }
```

Not `TestServiceResolution` or generic `Test1`/`ShouldWork` names — the name should read as a
one-line spec of the behavior under test.

## 2. Structure: Arrange-Act-Assert

Comment each section on multi-statement tests; collapse to an expression-bodied `=>` only for
trivial single-assertion `[Theory]` cases:

```csharp
[Fact]
public void HeroBuilder_SetsRoleType_WhenAssassinCategorySpecified()
{
    // Arrange
    var builder = new HeroBuilder().WithCategory(CategoryType.Physical);

    // Act
    var hero = builder.WithRole(RoleType.Assassin).Build();

    // Assert
    hero.RoleType.ShouldBe(RoleType.Assassin);
}

[Theory]
[InlineData(HeroDifficultyType.easy, false)]
[InlineData(HeroDifficultyType.superHard, true)]
public void HeroDifficulty_IsAdvanced_MatchesExpected(HeroDifficultyType difficulty, bool expected)
    => difficulty.IsAdvanced().ShouldBe(expected);
```

## 3. Data-driven tests over near-duplicate `[Fact]`s

`[Theory]` + `[InlineData]` for simple value sets, `[MemberData]` when cases need real objects.
See `arcane-testing-principles` §2/§3 for when to reach for this and the League of Legends theming
convention (`Hero`/`RoleType`/`CategoryType`/`HeroDifficultyType` — extend these fixtures instead
of inventing new generic ones).

## 4. Avoid boilerplate: helpers over copy-paste

Extract repeated setup (seen more than twice) into a **local static factory helper** — prefer this
over instance fields for stateless setup. Reach for `IClassFixture<T>` only for genuinely expensive
shared fixtures (`WebApplicationFactory`, a Testcontainers-backed factory) — not as the default for
ordinary setup. Don't extract when the setup variation between tests _is_ what's under test.

## 5. Async: always `async Task`, never `async void`

`async void` swallows exceptions outside the test-runner's `try`/`catch` — a failing assertion in
an `async void` test can pass silently.

## 6. Scope & tenant wiring

Every test gets a **fresh `ServiceProvider` scope** — never share one across tests, even in the
same class, to avoid state bleeding between them. Set the tenant via the `SetTenant` extension
helper (`TestHelpers.cs`) against `TenantAccessor<TTenant>` for unit-level tests. For API
integration tests, the tenant is set via the `X-SSV-Tenant` request header instead, because
middleware resolves it from the header, not DI — setting it via `TenantAccessor` in an integration
test won't reach the code under test.

## 7. Test doubles: end of file, narrow visibility

Hand-written fakes/stubs go after a `// ---- Test doubles ----` separator at the bottom of the
file. `file`-scoped visibility if only that file uses them; promote to a shared `TestDoubles.cs`
once a second file needs the same double.

## 8. Integration tests

Every repo's entry point is `IClassFixture<WebApplicationFactory<Program>>` via a per-repo
subclass (`VaultWebApplicationFactory`, `FoundryDbTestFactory`, `GemstoneWebApplicationFactory`,
blueprint's `Testcontainers.MsSql`-backed factory) — see `arcane-dotnet-aspnet-conventions` §5 for
the actual class shape and Testcontainers setup, don't hand-roll a fresh host. Real-DB tests are
tagged `[Trait("Category", "e2e")]` and excluded from the fast run via `--filter
"Category!=e2e"`.

## 9. Project structure

Test projects live under `test/` (or `tests/` in cosmowrench.api; repo-root
`Arcane.Server.Tests/` in vault/foundry/gemstone), named `Arcane.<Domain>.Test(s)`. Shared test
utilities/builders live in `src/Arcane.Test.Utils` (`arcane.dotnet`) — check there before writing a
new builder/factory helper from scratch.

## Quick Reference Checklist

- [ ] Assertion library matches what the target project/repo already uses (§0) — don't mix Shouldly and FluentAssertions in one class
- [ ] Test name reads as `Subject_WhenCondition_ExpectedOutcome`, not generic (§1)
- [ ] AAA-commented, or expression-bodied only for trivial `[Theory]` cases (§2)
- [ ] 3+ near-duplicate `[Fact]`s → one `[Theory]`/`[InlineData]`/`[MemberData]` (§3)
- [ ] Repeated setup extracted to a static helper; `IClassFixture<T>` reserved for expensive fixtures (§4)
- [ ] `async Task`, never `async void` (§5)
- [ ] Fresh DI scope per test; tenant set via `SetTenant`/`TenantAccessor` (unit) or `X-SSV-Tenant` header (integration) (§6)
- [ ] Test doubles at end of file behind `// ---- Test doubles ----`, `file`-scoped unless shared (§7)
- [ ] Integration tests extend the repo's `WebApplicationFactory` subclass; real-DB tests tagged `Category=e2e` (§8)
