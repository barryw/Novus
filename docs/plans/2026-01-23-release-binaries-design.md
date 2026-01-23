# Release Binaries Pipeline Design

## Overview

Build AOT-compiled Novus binaries for Windows, macOS, and Linux, and attach them to GitHub releases automatically when version tags are pushed.

## Goals

- Single binary distribution (no runtime dependencies)
- Support all major platforms: Linux x64, macOS x64, macOS arm64, Windows x64
- Avoid pipeline code duplication using Woodpecker matrix builds
- Automatic attachment to GitHub releases with SHA256 checksums

## Architecture

### Trigger

Pipeline triggers on tag pushes matching `v*` (e.g., `v0.2.0`). Integrates with existing `cog bump --auto` workflow.

### Matrix Configuration

| OS | Arch | Runtime ID | Agent Label | Binary Name |
|----|------|------------|-------------|-------------|
| Linux | x64 | `linux-x64` | `linux/amd64` | `novus-linux-x64` |
| macOS | x64 | `osx-x64` | `darwin/amd64` | `novus-macos-x64` |
| macOS | arm64 | `osx-arm64` | `darwin/arm64` | `novus-macos-arm64` |
| Windows | x64 | `win-x64` | `windows/amd64` | `novus-windows-x64.exe` |

### Build Flow

```
Tag pushed (v0.2.0)
       │
       ▼
┌──────────────────────────────────────────────────────┐
│              Matrix Jobs (parallel)                   │
├─────────────┬─────────────┬─────────────┬────────────┤
│ Linux x64   │ macOS x64   │ macOS arm64 │ Windows    │
│ (linux agent)│ (mac agent) │ (mac agent) │ (win agent)│
└─────────────┴─────────────┴─────────────┴────────────┘
       │
       ▼ (all complete)
┌──────────────────────────────────────────────────────┐
│              Release Step (linux agent)               │
│  - Collect all binaries                              │
│  - Generate checksums.txt                            │
│  - gh release create + upload                        │
└──────────────────────────────────────────────────────┘
```

### Build Command

Each matrix job runs:
```bash
dotnet publish Novus/Novus.csproj -c Release -r <rid> -o ./publish
```

The existing `<PublishAot>true</PublishAot>` in the csproj handles AOT compilation.

## Pipeline File

**Location:** `.woodpecker/build-release.yml`

### Matrix Jobs (build step)

Each job:
1. Checks out code at the tagged commit
2. Runs `dotnet publish` with platform-specific runtime ID
3. Renames output to standard name (`novus-<os>-<arch>`)
4. Stores binary for collection by release step

### Release Step

Runs once after all matrix jobs complete:
1. Collects all platform binaries
2. Generates `checksums.txt` with SHA256 hashes
3. Creates GitHub release: `gh release create <tag> --generate-notes`
4. Uploads all assets: `gh release upload <tag> novus-* checksums.txt`

## Agent Requirements

All Woodpecker agents need:
- .NET 9 SDK installed
- `gh` CLI installed and authenticated (or use `GITHUB_TOKEN` secret)

Agent labels:
- Linux: `platform: linux/amd64`
- macOS Intel: `platform: darwin/amd64`
- macOS Apple Silicon: `platform: darwin/arm64`
- Windows: `platform: windows/amd64`

## Secrets

| Secret | Purpose |
|--------|---------|
| `github_token` | GitHub token with `contents:write` for release creation/upload |

## Binary Naming Convention

Simple names without version (version is implicit in the release):
- `novus-linux-x64`
- `novus-macos-x64`
- `novus-macos-arm64`
- `novus-windows-x64.exe`

## Checksum Format

`checksums.txt` contains SHA256 hashes:
```
abc123...  novus-linux-x64
def456...  novus-macos-x64
789ghi...  novus-macos-arm64
jkl012...  novus-windows-x64.exe
```

## Integration with Existing Workflow

1. Developer runs `cog bump --auto` (or CI triggers it)
2. Cog analyzes commits, bumps version, updates .csproj files, creates tag
3. Tag push triggers build-release.yml
4. Matrix builds run in parallel on native agents
5. Release step creates GitHub release with all binaries

## Future Considerations

- Add Linux arm64 if demand exists
- Consider code signing for macOS/Windows binaries
- Homebrew formula / Scoop manifest for package manager installs
