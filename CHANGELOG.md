# Changelog

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

## [0.1.5] - 2026-11-04

### Added

- Observability via NuGet `OxyKit.Observability` (JSON console + `X-Correlation-Id`; no OTel package)
- Template Scriban helpers: `qr` / `barcode` (inline SVG); `money` / `number` / `percent` / `date` / `datetime`; `asset` (template-folder → `data:` img); `include` (path-safe `.html` partials); invoice starter (logo, footer partial, `@page` chrome, multi-page `sample-data.json`); print-baseline `.pf-header` / `.pf-footer` / `.pf-layout`; `qrcoder@` + `barcoder@` pins in `EngineIdentity`

### Changed

- Release workflow: `v*` always ships host zips; nuget.org only when Client/Contracts `<Version>` matches the tag (Api + Core carry the host version)

## [0.1.0] - 2026-10-22

### Added

- Engine identity on every successful render: `X-OxyKit-Render-Version` + `X-OxyKit-Render-Fingerprint` (Core version + SHA-256 of output-affecting pins / print baseline); same fields on `/health`
- `RenderClient` / `RenderFileResult` surface `EngineVersion` and `EngineFingerprint`
- Hoppscotch smoke (`hoppscotch/`)
- Scalar UI at `/scalar` (Development) alongside `/openapi/v1.json`
- Suite HOW-TO: .NET Framework 2.0 (`HttpWebRequest`) / 4.5+ (`HttpClient`) render ([`docs/DOTNET_FRAMEWORK_INTEGRATION.md`](../docs/DOTNET_FRAMEWORK_INTEGRATION.md))
- Packable `OxyKit.Render.Client` + `OxyKit.Render.Contracts` (`net10.0`); Trusted Publishing release workflow (`.github/workflows/release-nuget.yml`)

### Changed

- OpenAPI + Scalar gated to **Development** (suite OpenAPI convention)
- Tests: **xunit.v3.mtp-v2** (`OutputType=Exe`, `global.json` → Microsoft.Testing.Platform); **FluentAssertions** (suite pin)
