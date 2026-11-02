# AutoCommand — v2026.10.31.2

The Shields Up tab stops being one long scroll: the rituals are now five
focused sub-tabs — Quick Scan, Doors, Privacy, Junk, Hardening — and the
firewall preset buttons are gone from it, because they were a duplicate of
the Firewall tab's own profile control.

## What changed

- **Sub-tabs.** One sidebar item, five pages inside it, each answering one
  question: ✅ **Quick Scan** (the whole ritual as one checklist — still the
  default landing), 🚪 **Doors** (SSTP/miniports, Wi-Fi Direct, KDNET, VPN
  services, WAN miniports), 📵 **Privacy** (per-vendor opt-outs, hosts
  pinning, revert), 🧹 **Junk** (bloatware scan + removal), 🔐 **Hardening**
  (IPv6, LSA, UAC, hosts audit). Switching sub-tabs re-reads that section's
  live state — opening a tab IS a check. The last-used sub-tab is
  remembered across launches (Quick Scan stays the default).
- **Firewall presets removed from the tab.** They duplicated the Firewall
  tab's profile control (same engine, same presets). The Firewall tab now
  also pauses the drift enforcer for the apply window before re-capturing
  the baseline — the atomic-apply behavior the Shields Up copy had, moved
  to where presets actually live. Quick Scan keeps its Shield Up profile
  step.
- **Results stay on their tab.** The OS-hardening actions used to report
  into the doors card's result line; they now have their own, so feedback
  is never on a tab you're not looking at.
- **Honest pointers.** The nav tooltip and the `apply_shieldsup_preset`
  bridge tool description now say the firewall profile lives in the
  Firewall tab (tool name unchanged — registered MCP clients unaffected).
