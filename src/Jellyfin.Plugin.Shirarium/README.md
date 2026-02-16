# Jellyfin.Plugin.Shirarium

Current plugin functionality:
- Plugin metadata and config
- Engine HTTP client for filename parsing
- Post-library-scan dry-run task for unmatched candidates
- Suggestion snapshot persistence (`dryrun-suggestions.json`)
- Admin API:
  - `POST /Shirarium/scan`
  - `GET /Shirarium/suggestions`

This remains non-destructive and does not rename/move files.
