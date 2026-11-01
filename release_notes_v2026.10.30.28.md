# AutoCommand III — v2026.10.30.28

Traffic Peek learns your subject: peer into a program instance, or pin a
service by name and let the window follow it across restarts.

## What changed

- **Three scopes, one peek.** The context menu now offers **Peer into this
  program (PID N)** — flows attributed to that instance via Sysmon — alongside
  the existing per-IP peek. One window per subject; a second request focuses
  the existing one.
- **⭐ Track this service.** On a svchost row, pin the hosted service by NAME
  (DiagTrack, CryptSvc…). A PID pin goes stale the moment the service
  restarts — the name doesn't. A tracked peek follows whichever svchost
  instance hosts the pinned service next, with zero user action on respawn.
- **🚀 Launch under watch…** Every peek window can start a program with
  SSLKEYLOGFILE set in its environment only, and opens a program-scoped peek
  for the new instance immediately — its TLS traffic decrypts as it connects
  (Chromium/Electron, curl, git, OpenSSL- and Python-based tools honor it).
  The key path is the recreated `AutoCommand.Intercept.Core` service
  (`TransmissionKeyService`), strictly local, per-program by default.
- **The coverage note.** On every "encrypted (no keys)" flow the window now
  says exactly how plaintext becomes possible and why it never will for
  schannel system components — instead of leaving an empty pane to interpret.
- **Engine.** `FlowDossierService` takes a scope (`remote ip` / `program` /
  `service`), with shared-host protection: a scope on svchost.exe is matched
  only by its exact PID, never by image. The offline NetWatch suite drives all
  three scopes end-to-end (16 new checks), including the restart-follows
  service case and the shared-host guard.
