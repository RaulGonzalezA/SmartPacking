# SmartPacking Repository Instructions

These instructions apply to the complete repository. A nearer `AGENTS.md`
complements these rules and takes precedence when more specific.

## Repository

- Solution: `SmartPacking.slnx`
- Runtime: .NET 10
- Language: C# with nullable reference types enabled
- Web host: ASP.NET Core minimal API with a lightweight web interface
- Tests: xUnit, FluentAssertions
- Persistence: EF Core with SQLite for local development

SmartPacking creates packing recommendations from a user's wardrobe, trips,
weather ranges, activities and the availability of clothing. Preserve stored
data and API responses unless the task explicitly changes their contract.

## Structure and Dependencies

```text
Api -> Application -> Domain
Api -> Infrastructure -> Application -> Domain
tests -> Application -> Domain
```

- `Domain` contains business concepts and has no outer-layer dependencies.
- `Application` owns recommendation rules and storage contracts; it must not
  reference API or Infrastructure.
- `Infrastructure` implements EF Core storage and other external boundaries.
- `Api` is the composition root, API surface and browser UI host.

Do not create, remove or relocate projects unless the task requires it.
Do not introduce an application dependency on EF Core, HTTP, filesystems or
the web host.

## Data and Compatibility

- The local SQLite database is generated at runtime; never commit it.
- Do not hardcode production connection strings, credentials or API tokens.
- Treat public endpoints, JSON property names and persisted entity fields as
  compatibility-sensitive.
- Add migrations before changing a persisted EF Core schema. `EnsureCreated`
  is suitable only while the local development schema remains disposable.
- Avoid persistence calls inside recommendation loops.

## Coding

- Follow existing C# naming and nullable conventions.
- Use constructor injection for dependencies.
- Keep recommendation scoring deterministic and explainable.
- Propagate `CancellationToken` through asynchronous store and HTTP boundaries.
- Use structured logging for meaningful operational events; do not log personal
  data, tokens or connection strings.
- Keep browser JavaScript focused on rendering and interaction. User-visible
  failures must show a clear message rather than fail silently.

## Commands

Restore:

```powershell
dotnet restore SmartPacking.slnx
```

Build:

```powershell
dotnet build SmartPacking.slnx --configuration Release --no-restore
```

Formatting verification (mandatory before completing C# or Razor changes):

```powershell
dotnet format SmartPacking.slnx --verify-no-changes --no-restore
```

If it fails, run `dotnet format SmartPacking.slnx --no-restore` and verify it
again. Do not deliver formatting or analyzer failures.

Tests:

```powershell
dotnet test SmartPacking.slnx --configuration Release --no-build
```

Run the app:

```powershell
dotnet run --project src\SmartPacking.Api
```

## Definition of Done

- Formatting verification passes for C# or Razor changes.
- Affected projects build without new warnings.
- Relevant tests pass.
- API and persisted-data changes are explicitly reported.
- UI changes are tested in a browser when they affect interaction.
- No generated database, build artifact or local IDE state is committed.
