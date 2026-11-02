# AutoCommand — v2026.11.2

This release adds Spawn Watches: you describe a class of processes once
("anything spawned by svchost that isn't a known helper") and AutoCommand
remembers matching spawns the moment Sysmon archives them — instead of you
chasing them after the fact, when the processes are already gone. The
Shields Up sub-tabs also stop being pill buttons and become a rail that
mirrors the app sidebar, with each section's live state on it.

## What changed

- **Spawn Watches (new tab, WATCH group).** A watch is a JSON rule — image
  and parent-image wildcards, parent svchost `-s` service scoping, a
  command-line substring, and allowlist subtraction (one pattern per line in
  `allowlist.txt`) — evaluated at *ingestion time*: the moment an event is
  written to the Sysmon archive, not when you query. Every hit lands in a
  per-day JSONL index under `%LOCALAPPDATA%\AutoCommand\watches\hits\` with
  the full EventData, so a hit is a complete substitute for the raw archive
  row. The tab shows rule cards (hit counts, enable/delete) beside the hit
  feed with a detail pane, and creating or editing a rule replays the last
  24 h so it covers history immediately instead of waiting for the next
  spawn. Files are the interface: hand-edit or drop in a rule JSON and it is
  picked up on the next pass, no restart.
- **Two new read-only bridge tools.** `list_watches` (rules with match
  definitions and 24 h hit counts) and `tail_watches` (recent hits with
  rule, image, PID, parent, service and command line) let an AI session
  answer "what did svchost spawn" for the classes you prefiltered in one
  call, instead of paging the raw archive.
- **Shields Up rail.** The five ritual sub-tabs render as a left-side rail
  mirroring the app sidebar (indicator bar, nav brushes, collapse to
  icons), and each row carries its section's live state as one dot —
  kernel debug red, open doors amber, all-closed green — refreshed on every
  section read, so the rail doubles as a checklist visible from every
  sub-tab.
- **Under the hood.** The Sysmon archive gained an `Appended` event on its
  single write path (live watch and backfill alike); spawn-watch matching
  runs on a consumer task so the archive lock never waits on rule files.
  WatchSelfTest joins the self-test family: it drives the real hook offline
  against redirected persistence — wildcards, service scoping, allowlist
  subtraction, hook → consumer → hits index, dropped-in rule pickup, replay
  dedupe.
