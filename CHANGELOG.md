# Changelog

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

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
