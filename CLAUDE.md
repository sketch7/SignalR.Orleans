# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository overview

**Sketch7.SignalR.Orleans** is a standalone, open-source-style .NET library — a fork of `OrleansContrib/SignalR.Orleans` — published as NuGet packages (`Sketch7.SignalR.Orleans`, `Sketch7.SignalR.Orleans.AspNet`). It implements a SignalR **backplane** on top of [Microsoft Orleans](https://github.com/dotnet/orleans), letting SignalR scale out across multiple servers using Orleans grains and streams instead of a classic backplane (e.g. Redis/SQL). It is used by Sketch7's "Arcane" platform as an external dependency, but treat this repo as its own independent project — README.md is the primary source of truth for public API usage, versioning follows its own scheme (see the dotnet/Orleans support matrix in README.md), and there is no coupling to Arcane-specific code here.

Target framework: `net10.0` (see `Directory.Build.props`). Package versioning is driven by `package.json`'s `version` field (used by `tools/version-builder.sh` for CI packing, not for npm).

## Commands

Build:
```
dotnet build SignalR.Orleans.slnx
```

Run all tests (from repo root):
```
dotnet test test/SignalR.Orleans.Tests/SignalR.Orleans.Tests.csproj
```

Run a single test:
```
dotnet test test/SignalR.Orleans.Tests/SignalR.Orleans.Tests.csproj --filter "FullyQualifiedName~OrleansHubLifetimeManagerTests.MethodName"
```

The `npm test` script (`package.json`) mirrors CI: `dotnet test --no-build -c Release --filter Category!=e2e` (run after a Release build). Note there are currently no tests tagged with an `e2e` category/trait in the test project, so this filter is effectively a no-op today.

Tests are xunit-based and spin up **real** in-process Orleans silo + client hosts via `OrleansFixture` (`test/SignalR.Orleans.Tests/OrleansFixture.cs`), using `UseLocalhostClustering()` and in-memory storage/streams — not mocks. `OrleansHubLifetimeManagerTests` shares one `OrleansFixture` via `IClassFixture<OrleansFixture>`, so tests interact with a live grain cluster.

Packing (matches CI, requires `package.json` version):
```
bash tools/pack.sh
```
Publishing to NuGet (requires `SKETCH7_NUGET_API_KEY` env var) is `tools/publish.sh`; both source `tools/version-builder.sh` for version resolution from `package.json`.

CI/CD is delegated to reusable workflows in the `sketch7/.github` repo (`.github/workflows/ci.yml`, `cd.yml`) — there's no bespoke pipeline logic to read locally beyond which reusable workflow is invoked.

## Architecture

### Two packages, one dependency direction

- **`src/SignalR.Orleans`** — the core library: Orleans grains, streams, hosting extensions (`.UseSignalR()` for both `ISiloBuilder` and `IClientBuilder`), and `HubContext` for pushing to clients from outside a Hub (e.g. from a grain). References `Microsoft.Orleans.Sdk`, `Microsoft.Orleans.Streaming`, `Microsoft.Orleans.Persistence.Memory`, and `Microsoft.AspNetCore.App` (framework reference, for SignalR protocol types).
- **`src/SignalR.Orleans.AspNet`** — the ASP.NET Core glue: `OrleansHubLifetimeManager<THub>`, the actual `HubLifetimeManager<THub>` implementation registered via `AddOrleans()` on `ISignalRBuilder`. Project-references `SignalR.Orleans`.

A consumer app needs both packages: `SignalR.Orleans` gets wired into the Orleans **silo/client** hosting, `SignalR.Orleans.AspNet` gets wired into ASP.NET Core's `AddSignalR()`.

### Wiring (see README.md for the canonical walkthrough)

- Silo: `siloBuilder.UseSignalR()` (`src/SignalR.Orleans/HostingExtensions.cs`) — registers the `PubSubStore` memory grain storage (overridable), the signalr storage provider, an `IConfigurationValidator` that fails fast if no `PubSubStore` is registered, and the `ORLEANS_SIGNALR_STREAM_PROVIDER` memory stream provider.
- Orleans client: `clientBuilder.UseSignalR()` (`src/SignalR.Orleans/Extensions.cs`) — adds the same memory stream provider on the client side.
- ASP.NET Core: `services.AddSignalR().AddOrleans()` (`src/SignalR.Orleans.AspNet/Extensions.cs`) — registers `OrleansHubLifetimeManager<>` as the open-generic `HubLifetimeManager<>`, plus an `IClusterClientProvider` (default resolves `IClusterClient` from DI; can be overridden e.g. for multi-cluster scenarios).

All provider/stream names live in `src/SignalR.Orleans/Constants.cs` (e.g. `PUBSUB_PROVIDER = "PubSubStore"`, `STORAGE_PROVIDER`, `STREAM_PROVIDER`, `SERVERS_STREAM`, `ALL_STREAM_ID`, `CLIENT_DISCONNECT_STREAM_ID`) — check here first when tracing how a message routes.

### Grain model (`src/SignalR.Orleans/{Clients,Core,Groups,Users}`)

Grain keys are strings composed as `{hubName}:{id}` (`ConnectionGrainKey.Build`, parsed back via `ConnectionGrainKey`), so each hub gets its own isolated namespace of grains:

- `ClientGrain` (`Clients/ClientGrain.cs`, key `hubName:connectionId`) — represents one SignalR connection. Tracks which server (`ServerId`, a `Guid` generated per `OrleansHubLifetimeManager` instance) currently owns the physical connection, and forwards `Send`/`SendOneWay` invocations to that server via a *replica stream* (see below). Subscribes to a `SERVER_DISCONNECTED` stream so it can self-cleanup if its owning server drops off the `ServerDirectoryGrain`. Has retry/give-up logic (`MaxFailAttempts = 3`) if the owning server is unreachable, after which it force-disconnects.
- `ConnectionGrain<TGrainState>` (`Core/ConnectionGrain.cs`) — abstract base for grains that group multiple connection ids together (`GroupGrain`, `UserGrain`). Maintains a `HashSet<string>` of connection ids, fans out `Send`/`SendExcept` by calling `ClientGrain.SendOneWay` on each member, and subscribes each member connection to its `CLIENT_DISCONNECT_STREAM_ID` stream so it self-removes on disconnect (with periodic cleanup of stale stream subscriptions via a grain timer).
  - `GroupGrain` (`Groups/`, key `hubName:groupName`) — SignalR groups.
  - `UserGrain` (`Users/`, key `hubName:userId`) — SignalR user-targeted sends (`HubConnectionContext.UserIdentifier`).
- `ServerDirectoryGrain` (`Core/ServerDirectoryGrain.cs`, singleton with key `0`) — tracks all live servers via periodic `Heartbeat(serverId)` calls from each `OrleansHubLifetimeManager`, and evicts + broadcasts a `SERVER_DISCONNECTED` stream event for servers that stop heartbeating (`SERVERDIRECTORY_CLEANUP_IN_MINUTES` = 3x the heartbeat interval).
- `HubContext` / `HubContext<THub>` (`Core/HubContext.cs`) — the entry point for pushing messages from **outside** a Hub (e.g. from a grain via `GrainFactory.GetHub<IMyHub>()`). Exposes `.Client(connectionId)`, `.Group(groupName)`, `.User(userId)` returning the respective grain interfaces to call `Send`.

### Message flow / stream sharding

`OrleansHubLifetimeManager<THub>` (`src/SignalR.Orleans.AspNet/OrleansHubLifetimeManager.cs`) is the `HubLifetimeManager<THub>` SignalR calls into for every `SendAllAsync`/`SendConnectionAsync`/`SendGroupAsync`/etc. It:
- Generates a random `_serverId` (Guid) per instance/process and heartbeats it to `ServerDirectoryGrain` on a timer.
- Subscribes to an `AllMessage` stream (per-hub, `ALL_STREAM_ID`) for `SendAllAsync`.
- Subscribes to a **replicated/sharded** `ClientMessage` stream (`StreamReplicaContainer`, `src/SignalR.Orleans/StreamExtensions.cs`) keyed by `SERVERS_STREAM` with `STREAM_SEND_REPLICAS = 10` shards, to receive messages destined for connections it owns locally. Sharding exists to avoid a single hot stream/server bottlenecking under high throughput (see CHANGELOG "perf: streams sharding").
- Maintains its own local `HubConnectionStore` (`_connections`) — sends to locally-held connections write directly (`SendLocal`); sends to connections owned elsewhere go through `ClientGrain.Send` (`SendExternal`), which streams to the owning server's shard.
- `IAsyncDisposable`/`ILifecycleParticipant<ISiloLifecycle>` — participates in silo lifecycle (`ServiceLifecycleStage.Active`) to lazily set up streams, and unregisters from `ServerDirectoryGrain` + unsubscribes all streams on shutdown.

Extension methods for wiring `IStreamProvider`/`IAsyncStream` conveniences (subscription resume/unsubscribe-all, replica stream resolution/partitioning) live in `src/SignalR.Orleans/StreamExtensions.cs`; grain/factory helpers (`GetClientGrain`, `GetGroupGrain`, `GetUserGrain`, `GetServerDirectoryGrain`, `GetHub`) live in `src/SignalR.Orleans/Core/GrainExtensions.cs`.

### C# language features in use

The codebase uses C# extension members (`extension(Type x) { ... }` blocks), e.g. in `Extensions.cs`, `GrainExtensions.cs`, `StreamExtensions.cs` — a newer syntax for extension methods/properties. Match this style rather than classic `static class X { static ... this T }` extension methods when adding new ones.

## Notable caveats (from CHANGELOG.md)

- `Send*` calls on connection/group/user grains are **fire-and-forget** (`SendOneWay`) by design since 2.0.0-rc4 — they do not await a response from the target server; don't assume delivery confirmation.
- Client grain state is only cleared on a graceful `hub-disconnect`; on other disconnect reasons state is intentionally kept so the grain/connection can potentially recover.
- 8.0.1 migrated from `RegisterTimer` to `RegisterGrainTimer` and stream ids to `StreamId.Create(namespace, key)` — if touching grain timers or streams, follow the patterns already used in `ServerDirectoryGrain`/`ConnectionGrain` rather than older Orleans APIs.
- The `PubSubStore` storage provider name is currently hardcoded as the *default* Orleans SignalR pub-sub provider name for backwards compatibility (see the `todo` comment in `Constants.cs`); overriding it is possible via `UseSignalR(cfg => cfg.Configure(...))` but the constant itself can't be renamed without a breaking change.
