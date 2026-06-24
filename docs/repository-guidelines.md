# Repository Guidelines

## Project Structure & Module Organization

`AgentForge.slnx` is the .NET solution root. Core projects live under `src/`: `AgentForge.AppHost` is the Aspire orchestration entry point, `AgentForge.WebApi` handles webhook/API traffic, `AgentForge.McpHost` hosts vertical tools, and `AgentForge.Verticals.*` contains shared vertical abstractions/hosting. The OpenWA API and dashboard source live in `src/OpenWA`, with the Vite dashboard under `src/OpenWA/dashboard`.

Vertical-specific prompts, data, tools, and assets belong in the vertical project. For Travel, use `src/Verticals/AgentForge.Verticals.Travel/{Configuration,Data,Tools,Assets}`. Do not move vertical assets back into `AgentForge.WebApi/wwwroot`.

Tests live in `tests/AgentForge.WebApi.Tests` and `tests/AgentForge.Verticals.Travel.Tests`.

## Build, Test, and Development Commands

- `aspire run`: start the distributed app through Aspire AppHost.
- `dotnet build AgentForge.slnx`: compile all .NET projects.
- `dotnet test AgentForge.slnx`: run xUnit v3 tests.
- `npm --prefix src/OpenWA test -- --runInBand`: run OpenWA Jest tests.
- `npm --prefix src/OpenWA run build`: build the NestJS OpenWA API.
- `npm --prefix src/OpenWA/dashboard run build`: build the Vite dashboard.

Use Aspire CLI and dashboard/logs for AppHost runtime troubleshooting instead of starting services manually.

## Coding Style & Naming Conventions

Use C# 14/.NET 10 conventions: nullable enabled, file-scoped namespaces, PascalCase for public types/members, camelCase for locals/parameters, and async methods ending in `Async`. Keep services small and constructor-injected. Prefer vertical abstractions over hard-coded Travel/WebApi coupling.

TypeScript in OpenWA follows the existing NestJS/Vite style. Use `npm --prefix src/OpenWA run lint` when changing API TypeScript.

## Testing Guidelines

.NET tests use xUnit v3 with test classes named `*Tests` and methods named `Method_condition_expectedResult`. Add focused tests for parser, dispatch, payload, configuration, and vertical asset behavior. OpenWA tests use Jest specs under `src/OpenWA/src`.

Before pushing meaningful changes, run the relevant .NET tests plus OpenWA API/dashboard builds when those areas are affected.

## Commit & Pull Request Guidelines

Recent commits use concise imperative subjects, for example `Add vertical media handling for travel bot` and `Fix OpenWA webhook queue Redis wiring`. Keep commits scoped and avoid mixing unrelated formatting or generated outputs.

Pull requests should include a short summary, validation commands/results, linked issue when applicable, and screenshots only for dashboard/UI changes. Note any Aspire resource, environment variable, migration, or vertical asset changes explicitly.

## Security & Configuration Tips

Treat AppHost as the source of truth for infrastructure wiring. Do not commit secrets, API keys, session data, or local OpenWA runtime state. Media URLs emitted by agents should come from approved vertical tools and assets, not free-form model guesses.
