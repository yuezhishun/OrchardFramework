# Repository Guidelines

## Project Structure & Module Organization
Current repository state is documentation-first: the main artifact is `SaaS平台功能需求文档（SQLite 版本）.md`.
Store product and architecture notes in `docs/` (or root while the repo is small).
When implementation is added, follow this layout:
- `src/` for OrchardCore host and custom backend modules.
- `frontend/` for Vue + Vite client code.
- `tests/` for automated tests that mirror backend/frontend module boundaries.
Keep models and module behavior aligned with OrchardCore conventions; if local docs conflict, OrchardCore-compatible structures take priority.

## Build, Test, and Development Commands
There is no runnable app scaffold yet. After backend/frontend projects are added, use:
- `dotnet restore` to restore .NET dependencies.
- `dotnet build` to compile backend projects.
- `dotnet test` to run backend tests.
- `npm --prefix frontend install` to install frontend dependencies.
- `npm --prefix frontend run dev` to start the Vite dev server.
- `npm --prefix frontend run build` to produce frontend production assets.

## Coding Style & Naming Conventions
Use 4-space indentation for C# and 2-space indentation for Vue/TypeScript files.
Use `PascalCase` for C# types, `camelCase` for variables/parameters, and `UPPER_SNAKE_CASE` for constants.
Name Vue components in `PascalCase` (example: `TenantList.vue`) and composables as `useXxx.ts` (example: `useTenantFilters.ts`).
Prefer small, focused modules over large multipurpose files.

## Testing Guidelines
No test framework or coverage gate is committed yet.
When adding tests, use:
- `tests/<Project>.Tests/` for backend tests with filenames ending in `*Tests.cs`.
- `frontend/tests/` for frontend tests with filenames ending in `*.spec.ts`.
Include tenant-isolation, permission, and edge-case coverage for each new feature.

## Commit & Pull Request Guidelines
Current history uses short, imperative subjects (example: `Initial commit`).
Follow `<area>: <imperative summary>` where possible (example: `tenants: add tenant disable endpoint`).
PRs should include a concise description, linked issue/task, test evidence, and screenshots for UI changes.

## Security & Configuration Tips
Do not commit secrets; `.env` is ignored and should remain local-only.
Document new configuration keys and migration steps in the same PR that introduces them.
