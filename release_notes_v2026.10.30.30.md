# AutoCommand III — v2026.10.30.30

Layer 3 arrives in Traffic Peek: **🛡 Intercept this process** — the one honest
way to see what a program that can never write TLS keys (Windows services
included) actually sends.

## What changed

- **🛡 Intercept this process.** On any "encrypted (no keys)" flow in a
  program or tracked-service peek, one button starts a 15-minute intercept
  session: a local TLS-terminating proxy becomes the temporary system proxy
  (WinHTTP for services, WinINET for apps), the target's QUIC is blocked so
  it falls back to TCP, and only connections attributed to that exact PID are
  decrypted. Everything else is tunnelled through untouched. This is the
  plaintext path for schannel components — the wuauserv/Delivery-Optimization
  case the metadata view could never answer.
- **Visible and reversible.** An amber banner shows the session, its countdown
  and a ⏹ Stop now button; stopping (or closing the window, or the app, or the
  15-minute box) restores the proxy, removes the firewall rule and untrusts
  the CA. Every change is journaled before it is made — a crash mid-session is
  undone at the next app start. A corrupt journal is never guessed at.
- **Scoped trust.** The temporary CA is trusted machine-wide only while the
  session runs and removed by thumbprint at its end; certmgr shows it as
  "AutoCommand Intercept CA" whenever it is trusted. Certificate-pinned apps
  fail loudly during a session and are recorded as "refused intercept
  (pinning)" — a signal, not an error.
- **The rows land in the same peek.** Intercepted conversations appear in the
  flow list tagged "decrypted · intercept", with request/response bodies
  readable in place (text + hex) and exportable with 💾 Save bodies — in
  memory until you say so.
- **Self-test coverage.** A new strict `InterceptSelfTest` CI suite drives the
  journal round-trip, crash recovery (including the never-guess corrupt case)
  and the CA/leaf minting with DNS and IP SANs — all in a temp dir; the
  netsh/registry hooks themselves stay on the manual E2E pass.
