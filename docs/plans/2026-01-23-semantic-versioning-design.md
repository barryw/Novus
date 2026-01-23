# Semantic Versioning Design

**Date:** 2026-01-23
**Status:** Approved

## Overview

Implement semantic versioning for Novus using cocogitto (cog) with conventional commits. Version is stored in .csproj files and git tags, enforced via git hooks and CI, with automatic changelog generation.

## Configuration

### cog.toml

```toml
[settings]
from_tag = "v0.1.0"
tag_prefix = "v"
ignore_merge_commits = true
branch_whitelist = ["main"]

[changelog]
path = "CHANGELOG.md"
template = "default"
authors = [{ signature = "Claude Opus 4.5", username = "claude" }]

[[bump_hooks]]
command = "sed -i '' 's/<Version>.*<\\/Version>/<Version>{{version}}<\\/Version>/g' Novus/Novus.csproj Novus.Core/Novus.Core.csproj"
```

### Version in .csproj

Both `Novus/Novus.csproj` and `Novus.Core/Novus.Core.csproj` will include:

```xml
<PropertyGroup>
    <Version>0.1.0</Version>
</PropertyGroup>
```

## CI Integration

### .woodpecker/commits.yml

Validates conventional commits on every push:

```yaml
when:
  - event: push

labels:
  platform: linux/amd64

steps:
  - name: validate-commits
    image: ghcr.io/cocogitto/cog:latest
    commands:
      - cog check
      - echo "All commits follow conventional commit format"
```

### .woodpecker/release.yml

Manual release pipeline:

```yaml
when:
  - event: manual
    branch: main

labels:
  platform: linux/amd64

steps:
  - name: bump-version
    image: ghcr.io/cocogitto/cog:latest
    commands:
      - git config user.name "Woodpecker CI"
      - git config user.email "ci@novuslang.com"
      - cog bump --auto
      - git push --follow-tags
```

## Developer Workflow

### Installing the hook

```bash
cog install-hook commit-msg
```

### Conventional commit format

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

### Commit types

| Type | Bump | Description |
|------|------|-------------|
| `feat` | minor | New feature |
| `fix` | patch | Bug fix |
| `docs` | none | Documentation only |
| `refactor` | none | Code change without fix/feature |
| `test` | none | Adding/fixing tests |
| `chore` | none | Build, tooling, dependencies |
| `perf` | patch | Performance improvement |

### Breaking changes

Major version bumps are triggered by:
- `feat!:` or `fix!:` (exclamation mark suffix)
- `BREAKING CHANGE:` in commit footer

### Examples

```
feat(stdlib): add HashMap collection type
fix(codegen): resolve VBCC struct-by-value issue
docs(guide): update memory chapter
chore(deps): update ANTLR to 4.13.1
feat!: change array syntax to require size inference
```

## Version Display

Update `Novus/Program.cs` to display version:

```csharp
var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion ?? "unknown";
Console.WriteLine($"novus {version}");
```

## Implementation Steps

1. Add `<Version>0.1.0</Version>` to .csproj files
2. Create `cog.toml` configuration
3. Create initial git tag `v0.1.0`
4. Add `.woodpecker/commits.yml` and `.woodpecker/release.yml`
5. Create empty `CHANGELOG.md`
6. Update `Novus/Program.cs` with --version output
7. Document conventional commits in README or CONTRIBUTING.md

## Files Changed

| File | Action |
|------|--------|
| `cog.toml` | Create |
| `CHANGELOG.md` | Create |
| `.woodpecker/commits.yml` | Create |
| `.woodpecker/release.yml` | Create |
| `Novus/Novus.csproj` | Add Version property |
| `Novus.Core/Novus.Core.csproj` | Add Version property |
| `Novus/Program.cs` | Add --version output |
