# Source Project Instructions

These instructions complement the repository `AGENTS.md` for all projects in
`src`.

## Domain

- Keep `ClothingItem`, `Trip`, packing lists and enums independent of EF Core,
  ASP.NET Core, configuration and serialization details.
- Prefer immutable records for value-like concepts.
- Recommendation inputs must retain their meaning across API and persistence
  boundaries.

## Application

- Own recommendation rules, packing-list orchestration and interfaces such as
  `ISmartPackingStore`.
- Do not reference `SmartPacking.Api` or `SmartPacking.Infrastructure`.
- Keep scoring changes covered by deterministic tests. Explain the reason for
  a recommendation in the result rather than hiding decisions in the UI.

## Infrastructure

- Keep EF Core entities and mappings private to Infrastructure concerns.
- Use async EF Core methods and propagate cancellation.
- Query only the data needed, and preserve user ownership checks on reads and
  writes.
- Schema changes require a migration strategy; do not silently invalidate an
  existing local database.

## API

- Keep minimal API endpoints thin: validate input, resolve the current user,
  call Application services and return appropriate HTTP results.
- Do not expose EF Core entities directly.
- Keep `wwwroot` interactions accessible and provide visible success/error
  feedback for write operations.
- Do not put recommendation or persistence logic in `Program.cs`.
