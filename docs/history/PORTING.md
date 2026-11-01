# PORTING.md — upstream sync ledger

AutoCommand II is the rewrite staging repo; `dparksports/autocommand-windows`
(the original) keeps shipping to users until this replaces it. To keep the
two from diverging, after every II release window, diff the original's new
commits and port user-facing fixes/features into the cores here. Log every
port (and every deliberate skip) below.

## Window: 2026-10-27 (baseline + first five II releases)

Baseline: original @ `812fd51` (v2026.10.25.3). Checked through
original master @ that date.

| Upstream change | Status | Where it landed here |
|---|---|---|
| `638766c` RdapService: IANA-bootstrap routing + throttle-aware pacing (stop the rdap.org hammering; 429/503 cooldowns honoring Retry-After, negative-caching per prefix, RFC 1918 answered locally, User-Agent politeness) | **PORTED 2026-10-27** (this was the previously-mirrored urgent-RDAP work, now properly committed upstream in v2026.10.25.4) | `core/AutoCommand.NetWatch.Core/RdapService.cs` (file taken wholesale from `638766c`) + `Views/BlockedRulesWindow.xaml.cs` caller (`urgent: true`). App + Bits/NetWatch/Contain harnesses green. |
| urgent-RDAP feature follow-ups (anything newer than `638766c` in `RdapService`/`BlockedRulesWindow`) | open — still uncommitted in the original working tree | port when committed upstream |

## Window: CLOSED 2026-10-27 — the upstream freeze

`dparksports/autocommand-windows` froze at `c0ab6db` (v2026.10.25.4) as of
2026-10-27. AutoCommand II is the upstream; the porting loop is over. Every
user-facing change the original shipped is in this tree (the RDAP work is
content-identical modulo line endings; the release-notes/version-bump
commits were skipped per policy). Any bug reported against the frozen
original is fixed here and shipped in the next II release (fix-forward) —
never patched into the original. The original receives one docs-only
commit (the migration pointer) and no code, ever.

| Upstream change | Status | Where it landed here |
|---|---|---|
| `638766c` RdapService: IANA-bootstrap routing + throttle-aware pacing (stop the rdap.org hammering; 429/503 cooldowns honoring Retry-After, negative-caching per prefix, RFC 1918 answered locally, User-Agent politeness) | **PORTED 2026-10-27** (this was the previously-mirrored urgent-RDAP work, now properly committed upstream in v2026.10.25.4) | `core/AutoCommand.NetWatch.Core/RdapService.cs` (file taken wholesale from `638766c`) + `Views/BlockedRulesWindow.xaml.cs` caller (`urgent: true`). App + Bits/NetWatch/Contain harnesses green. |
| urgent-RDAP feature follow-ups (anything newer than `638766c` in `RdapService`/`BlockedRulesWindow`) | **CLOSED 2026-10-27** — verified nothing outstanding: original working tree clean at `c0ab6db`, the file is content-identical to the ported copy (CRLF/LF only) | — |

## How to port (historical — the loop is closed)

1. `git -C ../autocommand1004 log --oneline <last-sync-HEAD>..HEAD` — list
   upstream changes.
2. For each user-facing fix: apply to the owning core here (paths moved —
   see REWRITE.md's layout), run the harness that covers it, note it above.
3. Docs/notes-only changes: skip with a note.
4. Anything that changes the WPF shell here (Views/, App.xaml.cs): apply
   directly; the shell is shared lineage, not a core.
