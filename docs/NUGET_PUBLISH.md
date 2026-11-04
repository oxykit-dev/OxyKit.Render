# Publish Client + Contracts (nuget.org)

Trusted Publishing only — no long-lived `NUGET_API_KEY`.  
Workflow: [`.github/workflows/release-nuget.yml`](../.github/workflows/release-nuget.yml) (basename must match the nuget.org policy).

## One-time setup

### nuget.org

1. Sign in → **Trusted Publishing** → add policy:
   - Repository owner: `oxykit-dev`
   - Repository: `OxyKit.Render`
   - Workflow file: `release-nuget.yml` (filename only)
   - Environment: `nuget`
2. Prefer an organization owner if the GitHub org publishes long-term.

### GitHub (`oxykit-dev/OxyKit.Render`)

1. Environment: `nuget` (optional reviewers / wait timer). Used only when the tag also publishes Client/Contracts.
2. Secret `NUGET_USER` = nuget.org **profile username** (not email). Prefer environment-scoped secret.

Docs: [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

## Packages

| Package | Project |
|---------|---------|
| `OxyKit.Render.Contracts` | `src/OxyKit.Render.Contracts` |
| `OxyKit.Render.Client` | `src/OxyKit.Render.Client` |

Core is not packable. Host version lives on **Api** + **Core** (`<Version>`).

## Release

`v*` tags always ship the **host** (app zips + GitHub Release). NuGet publishes only when Client/Contracts versions match the tag.

### Host (always)

1. Bump `<Version>` on **Api** and **Core** (must match each other and the tag).
2. Cut `CHANGELOG.md`.
3. Merge to default branch.
4. `git tag vX.Y.Z && git push origin vX.Y.Z`

### NuGet (optional, same tag)

Also bump `<Version>` on **both** Client and Contracts to the **same** `X.Y.Z`. If they stay on an older version, the workflow skips nuget.org (host-only release).

GitHub Release assets:

- `OxyKit.Render.Api-{version}-{linux-x64|win-x64|osx-arm64}.zip` — runnable host per OS (framework-dependent; needs .NET 10 runtime)
- When publishing NuGet: `OxyKit.Render.Client-{version}-nuget.zip` plus individual `.nupkg` / `.snupkg`
