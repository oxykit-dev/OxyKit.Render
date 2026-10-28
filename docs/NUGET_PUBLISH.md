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

1. Environment: `nuget` (optional reviewers / wait timer).
2. Secret `NUGET_USER` = nuget.org **profile username** (not email). Prefer environment-scoped secret.

Docs: [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

## Packages

| Package | Project |
|---------|---------|
| `OxyKit.Render.Contracts` | `src/OxyKit.Render.Contracts` |
| `OxyKit.Render.Client` | `src/OxyKit.Render.Client` |

Core is not packable.

## Release

1. Bump `<Version>` in **both** Client and Contracts (must match).
2. Cut `CHANGELOG.md`.
3. Merge to default branch.
4. `git tag vX.Y.Z && git push origin vX.Y.Z` (tag without `v` must equal `<Version>`).

The workflow also creates a **GitHub Release** for the tag with:
- `OxyKit.Render.Api-{version}-{linux-x64|win-x64|osx-arm64}.zip` — runnable host per OS (framework-dependent; needs .NET 10 runtime)
- `OxyKit.Render.Client-{version}-nuget.zip` — Client + Contracts `.nupkg` / `.snupkg`
- individual NuGet package files as assets
