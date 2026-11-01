# AutoCommand III — v2026.10.30.29

Python environments can now be instrumented: spawn call stacks and TLS
session keys, surfaced in the scoped peek for exactly the process you're
looking at.

## What changed

- **🐍 Instrument…** Every Traffic Peek window gains the per-environment
  opt-in dialog (recreated from autocommand-ii): environments are discovered
  from the registry, PATH, Store, conda, uv, pyenv-win, scoop/choco, a bounded
  deep scan for venvs, and python binaries this machine has actually run
  (replayed from the Sysmon archive — nothing keeps watching). Instrumenting
  writes two small files into that environment's site-packages — nothing else
  changes, and removal is one click in the same dialog.
- **Python audit trace.** A program-scoped peek of a python process now shows
  that process's audit-hook records: the spawn APIs it called, their arguments,
  and the full python call stack at the moment each fired — the "why", scoped
  to the subject instead of a standing tab.
- **TLS keys land in the same peek.** Instrumented pythons log session keys to
  the same keylog the peek already tails, so their HTTPS traffic decrypts in
  the flows list — alongside the call stacks that explain it.
- **Payload seam fixed.** The hook payload is embedded in the same assembly
  that reads it (`AutoCommand.Forensics.Core`) — autocommand-ii carried a
  latent bug where the installer would have failed at runtime because the
  resource lived in the app exe.
- **Self-test coverage.** A new strict `PyTraceSelfTest` CI suite drives the
  installer round-trip, environment detection (incl. embedded-distribution
  refusal), the bounded venv scan, the reader's offset-resume tail, and the
  payload embed; `docs\selftest\run-selftest.ps1` revives the end-to-end
  harness: real python, real TLS session, decrypted via the hook's keylog.
