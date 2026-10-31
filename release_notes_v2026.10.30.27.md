# AutoCommand III — v2026.10.30.27

Process Monitor learns to look inside a connection: right-click → **Peer into this connection**.

## What changed

- **Traffic Peek window (Process Monitor context menu).** Any connection row can now
  be peered into, scoped to its remote IP. While the window is open, AutoCommand
  reassembles the TCP conversations to that IP and shows every finalized flow with
  what it actually carried — and nothing is captured once the window closes. One
  peek per IP; nothing is written to disk; capture, decryption and event reads are
  all local.
- **Honest TLS verdicts.** TLS metadata (server name, ALPN, cipher, version) is
  always extracted from the handshake. Decrypted plaintext appears only when session
  keys exist — and the window says exactly why when they don't: Windows services
  (wuauserv, BITS, DoSvc) never expose keys, so their rows stay metadata-only by
  design instead of showing empty content.
- **Service activity pane.** For the same time window, the window lists what the
  hosting service logged — Windows Update, BITS and Delivery Optimization operational
  events — the plaintext answer for connections whose TLS can never be decrypted.
- **Session-key logging toggle.** The keylog opt-out (previously a flag file) is now
  one button in the peek window: disabling deletes the keylog and stops recording
  keys; the dossier never recreates a file you turned off.
- **Self-test coverage.** The NetWatch suite now drives the whole peek pipeline
  offline: synthetic TLS/plain flows through the reassembler, remote-IP scoping,
  PID attribution, the no-keys honesty path, and the capture refcount lifecycle.

(For context: the dormant Transmissions engines from the pytrace era are what power
this — same reassembler, same TLS decryptor, now reachable from a row you're
actually curious about.)
