# Download history across the AutoCommand repositories

Snapshot taken `2026-11-01T03:25:12Z` (UTC), at the consolidation of AutoCommand III - following the
AutoCommand II consolidation into this same repository. Both AutoCommand II and III history, tags
and releases now live here.

> These numbers are **asset requests recorded by GitHub** - raw hits on release-asset URLs,
> not unique users. Source archives, clones, and mirrors are not counted. GitHub cannot
> migrate counters, so re-uploaded assets start at zero; this file preserves the true
> history. The full per-asset record lives in
> [download-history.json](download-history.json) (earlier captures kept in git history).

## Totals by repository

| repository | releases with assets | assets | recorded requests |
|---|---:|---:|---:|
| dparksports/autocommand-windows | 6 | 18 | 6 |
| dparksports/autocommand-ii-archive | 13 | 39 | 6 |
| dparksports/autocommand-iii | 1 | 3 | 5 |
| dparksports/autocommand-link | 1 | 3 | 0 |
| **total** | | **63** | **17** |

## Versions with recorded requests

| repo | version | zip requests | all assets |
|---|---|---:|---:|
| autocommand-windows | `v2026.10.20.6` | 1 | 2 |
| autocommand-windows | `v2026.10.21.1` | 2 | 3 |
| autocommand-windows | `v2026.10.25.4` | 1 | 1 |
| autocommand-ii-archive | `v2026.10.27.3` | 1 | 1 |
| autocommand-ii-archive | `v2026.10.27.7` | 1 | 2 |
| autocommand-ii-archive | `v2026.10.27.9` | 1 | 1 |
| autocommand-ii-archive | `v2026.10.28` | 1 | 1 |
| autocommand-ii-archive | `v2026.10.29.1` | 1 | 1 |
| autocommand-iii | `v2026.10.30.30` | 3 | 5 |

## Migration notes

- Releases from AutoCommand II and III were re-created here byte-identically; every zip
  matched the `SHA256SUMS.txt` published with it before upload.
- Releases whose binaries were pruned before the snapshots (39 on autocommand-windows,
  25 on autocommand-ii, 48 on autocommand-iii) remain tag-only history in the git tags
  and CHANGELOG.
- Old deep links keep working: the source repos are renamed (not deleted), so GitHub
  redirects `/releases/download/...` URLs.
