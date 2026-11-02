# AutoCommand — v2026.10.31.1

WSL stops being a mystery: the utility VM's traffic is now visible in the
Process Monitor, attributable to the Linux process that made it, and the
"WSL suddenly has no internet" case — the one that lives below the Windows
firewall rule model — has a one-click answer.

## What changed

- **WSL rows decode themselves.** Connections owned by the WSL family
  (wslservice, vmmem/vmmemWSL, wslrelay, wsl, wslhost, wslg) now show a
  `[WSL]` tag and a plain-English tooltip: *a Linux process inside the WSL
  utility VM made this connection; host-side attribution stops at the VM
  boundary* — plus the running distros and the networking mode (NAT vs
  mirrored, with its source). The panic-block failure mode dies here: the
  row says "your Linux VM did this" before anyone reaches for Block.
- **The Linux side answers back.** When the utility VM is already running,
  AutoCommand probes each distro's socket table (`ss -tnp`) and joins it to
  the host's WSL-family connections by destination (NAT preserves it): the
  tooltip grows a `linux side: node (pid 4312) → 1.1.1.1:80 (Ubuntu-22.04)`
  line. **The probes can never start the VM** — watching stays side-effect
  free; when the VM is down, the UI says so instead of launching it.
- **🩺 Why is WSL offline?** The Process Monitor's right-click menu gains a
  staged diagnosis (the 2026-10-30 field-report pipeline, promoted from
  PowerShell scripts into the product): host egress probe → guest probe
  (bash /dev/tcp, exit 0 vs 124) → the guest's NAT-gateway leg → the full
  firewall rule-model scan for WSL/Hyper-V-family rules → the Hyper-V
  firewall layer — ending in one plain-English verdict: healthy,
  machine-wide outage, disabled family rules, the Hyper-V layer blocking,
  or *a drop below the rule model* (WFP — not an AutoCommand block; the
  verdict hands you the elevated attribution step). Read-only end to end.
- **Blocking a WSL process warns like it should.** Blocking a family
  process takes down every distro's whole network — the confirmation now
  says so (its own "don't warn me" class, separate from the Windows-core
  warning) and points at the two narrower tools: block the remote IP, or
  the Hyper-V firewall layer.
- **Be My Guest learns the VM.** Read-only `get_wsl_status` (state, distros,
  mode, the guest socket join, the Hyper-V firewall profiles/rules — the
  Windows-native layer for per-VM containment) and `diagnose_wsl` (the same
  staged diagnosis the screen runs — the AI and the tab can never
  disagree). The offline proxy stub list ships both.
- **Self-test coverage.** A new strict `WslSelfTest` suite (in CI from this
  release): the family matcher, the wsl.exe UTF-16 list decoders, the
  .wslconfig mode parser, both real `ss` output shapes (Netid-first and
  State-first), the host→guest join, the guest-probe exit-code classifier,
  and every verdict branch of the diagnosis — all with synthetic inputs, no
  wsl.exe spawned, no socket opened.

## Fixed

- **The Shields Up tab no longer crashes on open.** v2026.10.31 shipped a
  latent bug in the vendored command runner: the async stderr drain ran
  under `using var`, and when the drain had not finished by the time the
  method returned, disposing it threw "A task may only be disposed if it
  is in a completion state" — caught by the door refresh on tab open and
  shown as a UI-error dialog. The drain now runs fire-and-forget (the
  deadlock guard stays; only the dispose is gone), confirmed against the
  crash.log stack from a real occurrence.
