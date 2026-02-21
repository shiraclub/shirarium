# Contributing to Shirarium

Thank you for contributing.

This project uses:
- Conventional Commits for commit messages
- `master` as the primary branch
- Local-first, non-destructive design principles

## Commit Message Style (Conventional Commits)

Use this format:

```text
<type>(<optional-scope>): <short summary>
```

Examples:

```text
feat(engine): add confidence threshold handling
fix(plugin): prevent null path crash in scanner
docs(readme): add CI badge and test instructions
test(engine): add fallback behavior coverage
chore(ci): run plugin build on pull requests
refactor(scanner): extract candidate filtering logic
```

Common types:
- `feat`: new feature
- `fix`: bug fix
- `docs`: documentation only
- `test`: tests only
- `refactor`: code change without behavior change
- `perf`: performance improvement
- `build`: build/dependency changes
- `ci`: workflow/pipeline changes
- `chore`: maintenance tasks

Optional scopes (recommended in this repo):
- `plugin`
- `engine`
- `scanner`
- `api`
- `scripts`
- `docs`
- `ci`

Breaking changes:
- Add `!` after type or scope, for example:
  - `feat(plugin)!: change suggestion snapshot schema`
- Also include a footer:

```text
BREAKING CHANGE: explain what changed and how to migrate.
```

## Pull Request Guidelines

1. Keep PRs focused and small when possible.
2. Include a clear description of:
   - what changed
   - why it changed
   - how it was tested
3. Update docs when behavior or workflows change.
4. Do not introduce destructive media file operations by default.
5. Keep cloud/LLM behavior optional and separated from core plugin behavior.

## Development Checklist

Before opening a PR, run what is relevant:

Hydrate Test Media:

```bash
python scripts/manage.py seed --dataset datasets/regression/tier-b-synthetic.json --clean
```

Plugin build:

```bash
dotnet build .\src\Jellyfin.Plugin.Shirarium\Jellyfin.Plugin.Shirarium.csproj -c Release
```

Plugin tests:

```bash
python scripts/manage.py test --integration
```

Direct plugin test commands:

```bash
dotnet test .\tests\Jellyfin.Plugin.Shirarium.Tests\Jellyfin.Plugin.Shirarium.Tests.csproj -c Release
dotnet test .\tests\Jellyfin.Plugin.Shirarium.IntegrationTests\Jellyfin.Plugin.Shirarium.IntegrationTests.csproj -c Release
```

Local stack smoke test:

```bash
python scripts/manage.py up
python scripts/manage.py reload
python scripts/manage.py api scan --token YOUR_TOKEN
python scripts/manage.py api plan --token YOUR_TOKEN
python scripts/manage.py api apply --fingerprint FINGERPRINT --token YOUR_TOKEN
python scripts/manage.py api status --token YOUR_TOKEN
python scripts/manage.py api undo --token YOUR_TOKEN
```

## Coding Expectations

- Prefer explicit, readable names over shorthand.
- Keep scanner and metadata application paths auditable.
- Keep dry-run behavior as the safe default.
- Add tests for new parsing or scan logic.

## Branching

- Primary branch: `master`
- Feature branches: any name is fine, for example:
  - `feat/scan-observability`
  - `fix/null-provider-ids`
  - `docs/contributing-guide`

## Release Workflow

To prepare a new release:

1.  **Bump Version**: Update the `<Version>` tag in `src/Jellyfin.Plugin.Shirarium/Jellyfin.Plugin.Shirarium.csproj`.
2.  **Update Changelog**: Add the new version header and summary of changes to `CHANGELOG.md`.
3.  **Clean Manifest**: Ensure `manifest.json` has an empty `"versions": []` array (if cleaning up) or simply trust the CI.

**Note on `manifest.json`**: This file is automatically populated by the GitHub Actions release workflow. Upon creating a new GitHub Release with a tag (e.g., `v0.0.13`), the CI will calculate the checksum, generate the download URL, and update the manifest in the `master` branch. Manual edits to the `versions` array in this file are typically not required.
