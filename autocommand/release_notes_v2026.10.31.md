# AutoCommand III — v2026.10.31

Shields Up moves in: the standalone admin console's four rituals — take the
junk out, set the firewall, close the doors, silence the phone-home — are now
a tab inside AutoCommand, driven by the same engine set, with the firewall
baseline re-captured atomically every time a preset applies.

## What changed

- **🔒 The Shields Up tab (CONTAIN).** Quick Scan is the whole ritual as one
  checklist: it reads your machine's current state first, shows what is
  already done in green, and finishes the rest in order behind one master
  confirmation — with every result re-verified against live state instead of
  trusted. Below it, the four sections: **doors** (SSTP VPN service +
  miniports, the Wi-Fi Direct ghost hotspot, kernel debug, the Windows VPN
  service family, WAN miniports), **firewall presets**, **telemetry
  opt-outs**, **bloatware**, and the **OS-hardening switches** (IPv6, LSA
  Protection, UAC strictness, hosts-file audit). Every action asks first; No
  is the default button; almost everything reverses.
- **One data home.** The tab points its state at `%PROGRAMDATA%\AutoCommand`,
  where the file names already matched by design — `firewall_config.json`
  becomes the one desired-state store the drift enforcer and the tab both
  read. If the standalone Shields Up app left state behind, the tab offers to
  import it (your edited bloatware list, the telemetry revert history, the
  update-freeze ledger); a running standalone app gets flagged before the two
  can fight over the same switches.
- **The WSL footgun is fixed at the source.** The 2026-10-30 field report
  caught a Shields-Up-era mass-disable whose narrow whitelist silently took
  WSL2/Hyper-V egress down below the rule model. The widened whitelist (mDNS,
  Core Networking + the VM-infrastructure groups: icsvc, WSL, Hyper-V, HNS,
  wslservice, vmcompute) now lives once, upstream, in the firewall engine —
  both C# copies delegate to it — and the checklist pins it offline.
- **Presets apply atomically.** Applying a preset pauses the drift enforcer,
  applies, and re-captures the firewall baseline in the same operation, so
  Auto-Mitigate protects the preset's intent instead of endlessly
  re-disabling rules Windows re-enables (the frozen-baseline footgun from the
  same report). The Shield Up whitelist spares VM networking.
- **Be My Guest learns the shields.** Read-only `get_shieldsup_status` (every
  door, hardening switch, hosts audit and per-vendor telemetry state), plus
  three gated tools: `close_shieldsup_door`, `opt_out_telemetry` and
  `apply_shieldsup_preset` — each confirmed in AutoCommand, each reversible,
  and deliberately no door-enable path over the bridge: reopening a door
  stays a human decision made in the tab. The offline proxy stub list ships
  the new tools so clients see them before a session starts.
- **Same engine, both faces.** The door/privacy/hardening checks were
  extracted from the WinUI pages into pure services that the tab drives —
  the standalone app and the tab cannot drift apart, and the standalone app
  keeps working for anyone who prefers it separate.
- **Self-test coverage.** A new strict `HardenSelfTest` suite (in CI from this
  release) pins the data-root seam, the shared-file reload-merge-write
  behavior, the import copy/merge/idempotency rules, the quick-scan shape and
  the widened whitelist — all offline against a temp data root.

## Notes

- `Agent.Core`, `AgentHost` and `ProxyInstallSelfTest` now target the same
  `net10.0-windows10.0.19041.0` floor the app and AgentProxy already carry —
  the vendored Shields Up core needs a Windows 10 SDK surface for the WinRT
  package manager. No supported-OS change: the app's floor was 19041 already.
- The archived `dparksports/autocommand-windows` README mirror updates as
  usual; the standalone Shields Up README now points here.
