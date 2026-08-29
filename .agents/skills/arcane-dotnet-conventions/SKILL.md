---
name: arcane-dotnet-conventions
description: "Use when writing or reviewing general-purpose C# code in an Arcane .NET repo (arcane.dotnet, hexgate, vault, foundry, gemstone, blueprint, cosmowrench.api) — covers language target, formatting (method chaining, constructor parameters, expression bodies), naming (record vs sealed class), guard clauses, C# 14 extension blocks, and extension-method namespace placement. For test conventions see arcane-dotnet-testing; for ErrorResult/ApiErrorException error handling see arcane-dotnet-errors; for controller/CQRS/DI conventions see arcane-dotnet-aspnet-conventions; for the .NET SSR frontend-host shell (blueprint.client, cosmowrench) see arcane-dotnet-fe."
---

> **Source of truth: this repo (`sketch7/arcane.archives`).** Edit here, then run `npx skills update` in consuming repos. Never edit the installed copy under a consumer repo's `.agents/skills/<name>/` — it's a pulled artifact and gets silently overwritten on the next sync.

# Arcane .NET Conventions

C# language and formatting conventions for Arcane .NET services, as practiced in `arcane.dotnet` —
the source of truth. A `.github/instructions/csharp.instructions.md` found in another repo that
doesn't match this skill is stale generic Copilot boilerplate predating it; delete it rather than
treating it as a second source.

## 1. Language & target

- `net10.0`, `LangVersion 14.0`, nullable reference types enabled globally, `ImplicitUsings` on
  (`Directory.Build.props`).
- No `!` null-forgiving suppressions without an explanatory comment — prefer `??` throw patterns
  (§4).
- Never leave warnings; don't suppress an inspection except the one deliberate case in §5.
- XML docs required on all public members.

## 2. Formatting

**Method chaining** — each chained call on its own line, indented one level; lambda bodies follow
the same rule (short lambdas inline, longer ones indented). Real example,
`Arcane.AspNet.Shared/ServiceCollectionExtensions.cs`:

```csharp
var mvcBuilder = services.AddControllers(opt =>
        {
            if (useKebabRoutingConvention)
                opt.Conventions.AddArcaneSlugifyParameterTransformer();

            opt.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        }
    )
    .AddJsonOptions(opts => opts.JsonSerializerOptions.WithArcaneApiDefaults())
    .AddHybridModelBinder()
;
```

**Constructor parameters** — each parameter on its own line, closing `)` on its own line;
attributes for a parameter go on the line immediately before it. Real example,
`Arcane.Orleans.DataStore.Contentful/ContentfulDeliveryClientGrain.cs`:

```csharp
public ContentfulDeliveryClientGrain(
    ILogger<ContentfulDeliveryClientGrain> logger,
    ILoggingContext loggingContext,
    IContentfulManagementClientResolver contentfulManagementClientResolver,
    [PersistentState("apiKeys", OrleansStoreNames.Crud)]
    IPersistentState<ContentfulApiKeyGrainState> apiKeyStore,
    IContentfulDataStoreConfigService contentfulDataStoreConfigService
) : base(logger, loggingContext)
```

**Expression bodies** — prefer `=>` for single-expression members. Arrow on the next line
(indented) when the expression is long; same line when it's short. Real example,
`Arcane.AspNet.Frontend/Language/LanguageService.cs`:

```csharp
public Task<List<LocaleInfo>> GetAll()
    => Task.FromResult(_languageConfig.Languages);

public LanguageConfigScheme GetConfig()
    => _languageConfig;
```

## 3. Naming & types

- `record` for immutable data/domain models; `init` setters, object-initializer syntax.
- `sealed class` for mutable stateful objects; apply `sealed` to every concrete implementation not
  meant for subclassing.

## 4. Guard clauses / null guards

Early returns over nested `if` — guard at the top, keep the happy path flat:

```csharp
public void Process(string key)
{
    if (key is null) return;
    if (!IsValid(key)) return;

    DoWork(key);
}
```

```csharp
ArgumentException.ThrowIfNullOrWhiteSpace(npmScript);
```

Null-coalescing throw, real example, `Arcane.Core/Caching/CachingService.cs`:

```csharp
var result = await _memoryCache.GetOrCreateAsync(key, entry => task());
return result ?? throw new InvalidOperationException($"Cache.GetOrCreateAsync returned null for key '{key}'");
```

For request/field-level API errors (`ErrorResult`, `ApiErrorException`) use `arcane-dotnet-errors`
instead of a raw exception — this section is for internal invariants only.

## 5. Extension method namespaces

Place extension methods in the namespace of the **extended type**, not the containing project's
namespace, so consumers discover them without an extra `using`. Real example,
`Arcane.AspNet.Shared/ArcaneServerDataStoreExtensions.cs` — the file lives in `Arcane.AspNet.Shared`
but `ArcaneServerBuilder` (the extended type) belongs to `Arcane.Microservice.Hosting`, so the
namespace follows the type, not the folder:

```csharp
// ReSharper disable once CheckNamespace
namespace Arcane.Microservice.Hosting;

public static class ArcaneServerDataStoreExtensions
{
    extension(ArcaneServerBuilder arcane) { ... }
}
```

Extensions on framework/third-party types (`IServiceCollection`, `IApplicationBuilder`,
`ISiloBuilder`) use the **package's own namespace** instead — you can't reuse Microsoft's. Either
way, suppress the namespace-mismatch inspection with `// ReSharper disable once CheckNamespace`
(the pattern used throughout `arcane.dotnet`) — this is the one deliberate suppression allowed by
§1.

## 6. C# 14 extension blocks

`extension(...)` blocks are the target shape for **new** extension methods — most of the codebase
still predates this and uses traditional `this`-parameter methods, which is fine to leave as-is
unless you're already touching that file:

```csharp
public static class MyExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMyService<TImpl>() where TImpl : class
        {
            services.AddScoped<TImpl>();
            return services;
        }
    }
}
```

> **Known SDK limitation (10.0.x):** an extension block with both a generic receiver
> (`extension<T>(Builder<T> b)`) and method-level generic parameters doesn't resolve — fall back to
> a traditional `this` extension method in that specific case.

## 7. Fluent builders

Builder methods always return `this` for chaining.

## Quick Reference Checklist

- [ ] Method chains / constructor params / expression bodies formatted per §2
- [ ] `record` for immutable data, `sealed class` for mutable stateful objects (§3)
- [ ] Guard clauses are early returns, not nested `if` (§4)
- [ ] Extension methods live in the extended type's namespace, not the project's (§5)
- [ ] New extension methods use C# 14 `extension(...)` blocks unless the SDK generic-receiver
      limitation applies (§6)
- [ ] No unexplained `!` suppressions, no new warnings, XML docs on public members (§1)
