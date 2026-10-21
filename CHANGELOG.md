# Changelog

Pre-release — everything lives under **`[Unreleased]`** until the first public SemVer tag.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added

- Engine identity on every successful render: `X-OxyKit-Render-Version` + `X-OxyKit-Render-Fingerprint` (Core version + SHA-256 of output-affecting pins / print baseline); same fields on `/health`
- `RenderClient` / `RenderFileResult` surface `EngineVersion` and `EngineFingerprint`
- Hoppscotch smoke (`hoppscotch/`)
- Scalar UI at `/scalar` (Development) alongside `/openapi/v1.json`
- Suite HOW-TO: .NET Framework 2.0 (`HttpWebRequest`) / 4.5+ (`HttpClient`) render ([`docs/DOTNET_FRAMEWORK_INTEGRATION.md`](../docs/DOTNET_FRAMEWORK_INTEGRATION.md))

### Changed

- OpenAPI + Scalar gated to **Development** (suite OpenAPI convention)
- Tests: **xunit.v3.mtp-v2** (`OutputType=Exe`, `global.json` → Microsoft.Testing.Platform); **FluentAssertions** (suite pin)
