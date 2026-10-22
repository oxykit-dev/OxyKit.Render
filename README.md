# OxyKit.Render

Document **render engine** for the [OxyKit](https://oxykit.dev) suite: an HTTP sidecar that turns JSON into PDF, XLSX, CSV, or HTML.

**License:** MIT · **Client:** [`OxyKit.Render.Client`](https://www.nuget.org/packages/OxyKit.Render.Client) `0.1.2` · **Org:** [oxykit-dev](https://github.com/oxykit-dev)  
**Integrators:** Client only (never `OxyKit.Render.Core`) · **Releases:** [app zips](https://github.com/oxykit-dev/OxyKit.Render/releases)  
**Site:** [oxykit.dev](https://oxykit.dev) · **Orchestrator:** [OxyKit.Flow](https://github.com/oxykit-dev/OxyKit.Flow)  
**.NET Framework 2.0 / 4.5+:** raw HTTP — see suite Framework HOW-TO on the docs site (no Client NuGet).

## Quick start

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/OxyKit.Render.Api
```

| URL / header | Purpose |
|--------------|---------|
| `http://localhost:5066` | API |
| `X-OxyKit-Render-Key: dev-local-key` | Dev API key (`appsettings.Development.json`) |
| `/health` | Anonymous |
| `/scalar`, `/openapi/v1.json` | OpenAPI UI + JSON (Development only) |
| `/formats`, `/templates`, `/render/{input}/{output}` | Authenticated (+ `/v1/...` aliases) |

```bash
dotnet test tests/OxyKit.Render.Api.Tests/OxyKit.Render.Api.Tests.csproj
dotnet test tests/OxyKit.Render.Client.Tests/OxyKit.Render.Client.Tests.csproj
```

**Smoke:** import [`hoppscotch/`](./hoppscotch/) collection + env → **OxyKit.Render Local** → run folders in order.

```bash
curl -X POST "http://localhost:5066/render/table/html" \
  -H "Content-Type: application/json" \
  -H "X-OxyKit-Render-Key: dev-local-key" \
  -d '{"sheets":[{"name":"Orders","headers":["Id","Customer","Total"],"rows":[[1,"Acme",120.5],[2,"Beta",80]]}]}' \
  --output orders.html
```

## NuGet (Client)

```bash
dotnet add package OxyKit.Render.Client
```

Also: `OxyKit.Render.Contracts` (transitive). Packable packages ship Client + Contracts only — Core stays host-internal. Releases: tag `v*` → [docs/NUGET_PUBLISH.md](./docs/NUGET_PUBLISH.md).

Local suite checkout (optional):

```xml
<ProjectReference Include="..\OxyKit.Render\src\OxyKit.Render.Client\OxyKit.Render.Client.csproj" />
```

## Transforms

| Route | Input | Output |
|-------|-------|--------|
| `POST /render/html/pdf` | raw HTML `content` | PDF |
| `POST /render/template/html` | `templateId` + JSON `data` | HTML |
| `POST /render/template/pdf` | `templateId` + JSON `data` | PDF |
| `POST /render/csv/pdf` | CSV `content` | PDF |
| `POST /render/csv/xlsx` | CSV `content` | XLSX |
| `POST /render/table/html` | structured `sheets` | HTML |
| `POST /render/table/pdf` | structured `sheets` | PDF |
| `POST /render/table/xlsx` | structured `sheets` | XLSX |
| `POST /render/table/csv` | exactly one sheet | CSV |

Errors: `{ "error": "..." }` (400 / 401 / 403 / 500). Layout and constraints: [AGENTS.md](./AGENTS.md).

## Docs

| Doc | Role |
|-----|------|
| [AGENTS.md](./AGENTS.md) | Layout, constraints, add-transform checklist |
| [hoppscotch/](./hoppscotch/) | Manual HTTP smoke |
| [CHANGELOG.md](./CHANGELOG.md) | Keep a Changelog |
| `/scalar` · `/openapi/v1.json` | Live API shapes (Development) |
| [LICENSE](./LICENSE) | MIT |
