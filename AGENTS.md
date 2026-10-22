# OxyKit.Render - agent guide

Token-efficient map. Prefer this over exploring the tree or loading the full README.

## What it is

**OxyKit** suite core. **MIT** · v0.1 — Client/Contracts packable (nuget.org via Trusted Publishing / `release-nuget.yml`); Core host-internal (`IsPackable=false`). See `docs/NUGET_PUBLISH.md`.

.NET 10 Minimal API **document render sidecar**: clients POST JSON -> PDF / XLSX / CSV / HTML. Local + API key by default. Shared contracts + HttpClient SDK for Local and suite consumers (e.g. **OxyKit.Flow**).

**Host choice:** stay on **Minimal APIs** (not FastEndpoints). Suite priority here is **resources + AOT-friendliness**; Mail/Flow use FE+ErrorOr for DX. Binary file responses and dual `/` + `/v1` `MapGroup` fit this stack.

## Open these files first

| Task | Read first |
|------|------------|
| Add/change transform | `OxyKit.Render.Core/DocumentRenderService.cs` -> `Api/Endpoints/RenderEndpoints.cs` -> matching `Inputs/` or `Outputs/` |
| Request DTOs / public shapes | `OxyKit.Render.Contracts/` |
| Typed HTTP SDK | `OxyKit.Render.Client/RenderClient.cs` |
| Table/CSV limits & typing | `Core/Inputs/TableWorkbookConstraints.cs`, `Core/IsoDateCell.cs`, `Core/Inputs/CsvTableParser.cs` |
| Templates | `Core/Inputs/TemplateStore.cs`, `TemplateDataValidator.cs`, `ScribanTemplateRenderer.cs` |
| PDF | `Core/Outputs/HtmlToPdfRenderer.cs`, `PdfPageOptions.cs`, `Contracts/PdfPageEnums.cs` |
| Print CSS | `Core/assets/print-baseline.css`, `Outputs/PrintBaselineInjector.cs` |
| Auth | `Api/Security/InternalAccessMiddleware.cs` |
| DI / limits | `Api/Program.cs`, `Core/RenderingOptions.cs`, `Core/ServiceCollectionExtensions.cs` |
| OpenAPI shapes | `Api/OpenApi/RenderRequestSchemaTransformers.cs` |
| Tests | `tests/OxyKit.Render.Api.Tests/`, `tests/OxyKit.Render.Client.Tests/` |
| Hoppscotch | `hoppscotch/` (do not paste JSON into chat) |
| Cursor rules | `.cursor/rules/` |

Ignore `bin/`, `obj/`, and copied `appsettings` under test output.

**Suite layout:** sibling of `OxyKit.Flow/` (and other suite cores) under `oxykit.suite/` (see `../AGENTS.md`).

## Layout

```text
OxyKit.Render.slnx
src/
  OxyKit.Render.Contracts/   public DTOs + document metadata shapes
  OxyKit.Render.Client/      HttpClient SDK (integrators + Flow -> engine)
  OxyKit.Render.Core/        rendering engine (Inputs/, Outputs/) - Local API only
  OxyKit.Render.Api/         Local/engine host: Endpoints/, Security/, OpenApi/, templates/
tests/
  OxyKit.Render.Api.Tests/
  OxyKit.Render.Client.Tests/
hoppscotch/
```

## Transforms (`SupportedFormats`)

`csv/pdf` `csv/xlsx` `html/pdf` `table/csv` `table/html` `table/pdf` `table/xlsx` `template/html` `template/pdf`

Route pattern: `POST /render/{input}/{output}` and aliases under `POST /v1/render/{input}/{output}`.
Also `GET /formats`, `/templates`, `/health` (anon), `/v1/health` (anon).  
Successful renders stamp `X-OxyKit-Render-Version` + `X-OxyKit-Render-Fingerprint` (see `EngineIdentity`, `RenderEngineHeaders`); `/health` returns the same as JSON.  
OpenAPI (Development only, anon): `/openapi/v1.json` + Scalar `/scalar` (suite convention).

## Add a transform (checklist)

1. Normalize under `Core/Inputs/` if needed
2. Emitter in `Core/Outputs/` -> `RenderResult`
3. Method on `DocumentRenderService` (use `ExecuteAsync` for gate + logging)
4. `MapPost` in `RenderEndpoints` via `ExecuteRender` (root + `/v1` via `MapGroup`)
5. Public request DTO in `OxyKit.Render.Contracts` if new
6. Client method on `RenderClient` if public
7. Append to `SupportedFormats`
8. OpenAPI schema transformer if new request DTO
9. Tests under matching `tests/...` folder (xUnit v3 / MTP; `dotnet test --project …`) + Hoppscotch request if public API
10. Note in `CHANGELOG.md` `[Unreleased]` when behavior changes

Validation failures -> `ArgumentException` / `NotSupportedException` -> HTTP 400 `{ "error": "..." }`.

## Hard constraints (do not regress)

- Keep Core/Api types `internal sealed` where practical; Local API tests use `InternalsVisibleTo`
- Public surface for integrators lives in `OxyKit.Render.Contracts` + `OxyKit.Render.Client`
- Framework 2.0 / 4.5+: no Client — [`../docs/DOTNET_FRAMEWORK_INTEGRATION.md`](../docs/DOTNET_FRAMEWORK_INTEGRATION.md)
- **Do not reference `OxyKit.Render.Core` from Flow or other consumers.** They use `OxyKit.Render.Client` -> OxyKit.Render API. No tenancy/billing/storage in Core.
- Template `data` is untrusted: JSON Schema + HTML-escape on interpolate
- PDF page options use OxyKit.Render `PdfPageSize` / `PdfPageOrientation` (not PeachPDF types); mapped in `PdfPageOptionsResolver`
- PeachPDF: no remote HTTP fetch; `AllowLocalFileAccess = false`; no `<link>` styles in templates - inline via `PrintBaselineInjector` / `<style>`
- Path-safe template ids only under `Rendering:TemplatesRoot`
- Table limits live in `TableWorkbookConstraints` (single source of truth)
- Auth/render errors use shared `ErrorResponse` (`{ "error": "..." }`); anonymous paths via `AnonymousPaths`
- Do not weaken `Security:ApiKey` startup validation or anonymous-path list without intent
- Render concurrency gate covers parse/validate/template work as well as emit

## Commands

```bash
cd OxyKit.Render
dotnet run --project src/OxyKit.Render.Api          # :5066
dotnet test tests/OxyKit.Render.Api.Tests/OxyKit.Render.Api.Tests.csproj
dotnet test tests/OxyKit.Render.Client.Tests/OxyKit.Render.Client.Tests.csproj
```

Dev key: `X-OxyKit-Render-Key: dev-local-key`.  
Hoppscotch: import `hoppscotch/*`, env **OxyKit.Render Local**, run folders in order.

## Config knobs

`Security:RequireLocalRequests`, `ApiKeyHeader`, `ApiKey` (startup fails if missing / `change-me-in-production`); `Rendering:MaxHtmlContentChars`, `MaxCsvContentChars`, `MaxRequestBodyBytes`, `MaxConcurrentRenders`, `TemplatesRoot`, `MaxTemplateChars`, `MaxContractChars`. Templates on disk: `templates/{id}/template.html` + `contract.json`. Table/CSV limits: `TableWorkbookConstraints`.

## Docs policy for agents

- Prefer this file over dumping README / OpenAPI / Hoppscotch JSON.
- Prefer editing the smallest owner: Contracts ≠ Client ≠ Core ≠ Api endpoints.
- Tenancy/billing/storage do not belong in Core/Api.
- Suite map: `../AGENTS.md`.
