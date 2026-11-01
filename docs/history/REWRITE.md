# AutoCommand II — rewrite plan

This repository is the AutoCommand rewrite. It began as a clean import of
`dparksports/autocommand-windows` @ `812fd51` (v2026.10.25.3 baseline,
fresh history) so the original repo stays untouched. The original keeps
shipping; everything here lands only when a phase is complete and green.

Two tracks, deliberately sequenced:

1. **UI plumbing fixes** (phase 0 — safe, behavior-preserving, started already)
2. **Core engine extraction** (phases 1+ — the structural rewrite: the ~70
   services leave the WPF exe for `*.Core` class libraries, following the
   in-repo pattern `SvchostAnalyzer.Core` and `OneSettingsCadence.Core`
   already established: one engine, many faces)

## Phase 0 — UI plumbing (in progress)

The measured problems this phase fixes (survey of the v2026.10.25.3 tree):

- All 14 tab views are constructed statically in `MainWindow.xaml`, so any
  timer started in a constructor ticks for the whole session.
- Views that poll with `Task.Run` + `Dispatcher.BeginInvoke` hand-roll the
  same ~40-line block (timer, reentrancy guard, thread hop, selection
  restore) per view.
- Full `ItemsSource = list` swaps regenerate every visual row per tick and
  force hand-coded selection restoration.

Delivered so far:

- **`Helpers/RefreshLoop.cs`** — one owner for timer + visibility gate +
  reentrancy guard + thread hop + single marshal. `RefreshLoop.Start(...)`
  for views built on the loop; `RefreshLoop.GateVisibility(view, () => timer)`
  as the minimal drop-in for a view that keeps its own tick logic.
- **`Helpers/ListSync.cs`** — `SyncByKey`: keyed in-place list sync;
  unchanged rows keep their containers and the user's selection survives
  reordering by construction.
- **PhoneHomeView** converted to both (the exemplar; `TraceCardVM` gained
  the value equality that makes "unchanged row" detectable).
- Visibility gates on the snapshot-poll timers that lacked them
  (TracesView 60 s, BlackHoleView 5 s, SvchostMonitorView block-state 60 s).
- BlackHoleView duplicate-subscription fix (Tick was re-subscribed on every
  tab re-entry, stacking one more RefreshAll per tick per visit).

### What may NOT be visibility-gated (learned the hard way — keep this list)

- `SvchostTraceView._poll` (1 s) — detects spawns by **diffing the live
  process set between ticks**; a paused tick loses the spawns that happened
  while hidden. Self-accumulating, not a snapshot read.
- `SvchostMonitorView._saveTimer` (1 h) — CSV persistence; must run while hidden.
- `SvchostMonitorView._lastSeenRefreshTimer` (10 s) — the row-enrichment pass
  (DNS enqueue, task attribution, svchost identity, archive reveals).
  Catch-up-safe in principle, but not worth the risk in the centerpiece view.
- `DefenderView._cpuDebounce` — a debounce delay, not a poll.
- One-shot timers (banner delays, toast auto-hides, status-text clearers).

Views that already self-gate via `Loaded`/`Unloaded` stop/start (leave them):
SpawnsView, BitsView, DefenderView, TaskHostSpawnView, BeMyGuestView,
TrafficPatternsView.

### Conversion playbook (per view)

1. Move the timer + guard + `Task.Run`/`BeginInvoke` block into
   `RefreshLoop.Start(this, interval, BuildRows, ApplyRows, onBegin: ...)`.
   `BuildRows` runs on the thread pool (no UI access, including
   `SelectedItem` — read selection state inside `ApplyRows`); `ApplyRows`
   runs on the UI thread. Button handlers force a pass with `_loop.Trigger()`.
2. For live tables: keep an `ObservableCollection<T>` field, bind
   `ItemsSource` once in the constructor, and refresh with
   `ListSync.SyncByKey(_rows, fresh, v => v.Key, RowList)`.
3. Give the row VM value equality (Equals/GetHashCode over rendered fields;
   exclude action handles like `TraceCardVM.What`) so unchanged rows are
   skipped entirely.

Next conversions by payoff: TransmissionsView (live feed, 1,068 lines),
SvchostMonitorView row pipeline, TracesView card refresh.

## Phases 1+ — core engine extraction (the structural rewrite)

### Phase 1 — delivered: `core/AutoCommand.NetWatch.Core`

Extracted (physically moved, namespaces deliberately unchanged —
`AutoCommand.Services` / `.Helpers` / `.Models` — so every consumer compiled
without edits):

- Services: TcpConnectionTable, IpClassifier, DnsResolutionService,
  RdapService, SysmonArchiveService, SysmonDiagnosticsService,
  SysmonInstallerService, SysmonProcessAuditService, SysmonWatcherService,
  ProcessGlossary
- Helpers: ProcessRunner
- Models: SvchostMonitorItem, IResolvableHost

Consumers now on the library: the app (ProjectReference),
NetWatchSelfTest (was: source-copying + a FrameworkReference hack — both
gone; ALL CHECKS PASSED against the shipped assembly, including the live
IPv6 loopback enumeration), BitsTraceSelfTest (IpClassifier/Rdap/Dns/
IResolvableHost now from the core; BITS-cluster sources still compiled
locally until their phase).

Deliberately NOT extracted yet (cross-cluster dependencies, move with
their clusters): SysmonConnectWatcherService (needs ImageSpawnWatcherService
— Forensics, and TcpStreamCaptureService — Intercept), BlockHitFeedService
(needs FirewallService — Contain).

**Lesson every future phase must repeat** (cost an afternoon here): a new
ProjectReference to a directory under the repo root is not enough — the
app's default Compile glob silently pulls the new core's `.cs` files into
the app assembly next to the ProjectReference (the repo already fought
this for svchost-watch/AgentProxy). Every extracted core needs its
matching `<Compile Remove="core\**" />` (already in AutoCommand.csproj for
the whole core\ tree — future cores under core\ inherit it for free).
Symptom when forgotten: the WPF _wpftmp markup pass fails with CS0579
duplicate-attribute errors that look like a versioning bug and are not.

Also new: `core/Directory.Build.props` (common settings for core\
projects only — deliberately not at the repo root so vendored engines and
self-test harnesses keep owning their settings).

### Phase 2 — delivered: `core/AutoCommand.Intercept.Core`

Extracted (19 files, namespaces unchanged): RawSocketSnifferService,
TcpStreamCaptureService, HttpStreamDecoder, TlsDecryptor, TlsKeylogService,
TlsSni, InterceptProxyService, InterceptCaService, InterceptSystemHooks,
TransmissionsPipelineService, TransmissionsArchiveService,
TransmissionsIgnoreList, TransmissionKeyService, TrafficAggregationService,
SysmonConnectWatcherService + models TcpFlowRecord, TlsDecryptResult,
TransmissionRecord, TrafficStat.

- Intercept core references the NetWatch core (RawSocketSnifferService
  builds SvchostMonitorItem rows) — first cross-core reference; the
  layering is: Intercept → NetWatch → (nothing).
- SysmonConnectWatcherService moved HERE, not to NetWatch: its phase-1
  "cross-cluster dependencies" (ImageSpawnWatcherService,
  TcpStreamCaptureService) turned out to be doc-comment mentions only,
  and its one real consumer is the transmissions pipeline in this cluster.
- Three members went internal → public to cross the assembly boundary
  (InterceptSystemHooks + RecoverAfterCrash, TrafficStat.FormatBytes) —
  the compiler finds these; that is the whole list.
- PyTraceSelfTest (stream capture/keylog/decrypt + their models now from
  the core; `reassemble` suite PASS) and SiteDoctorSelfTest
  (TransmissionRecord from the core; ALL PASS) run against the shipped
  assembly. NetWatchSelfTest and BitsTraceSelfTest re-run as regression:
  both still pass.
- Dependency-closure method worth repeating: scan candidates for
  references to every staying service/model, skipping comment lines —
  phase-1's comment-only false dependency is why SysmonConnectWatcher
  waited for phase 2.

### Phase 3 — delivered: `core/AutoCommand.Contain.Core` (test-first)

The stopping side: FirewallService + the BlocksStore undo ledger,
AppBlockCoverage, BlockHitFeed (unblocked at last), FirewallProfile,
TelemetryRulesService, HostsTelemetryPins, SinkholeService, the Defender
trio, UpdateFreeze, plus leaves SvchostIdentityService and ServiceCatalog,
and FirewallRuleItem extracted out of Models\DataModels.cs (the first
DataModels split — the type is pure INotifyPropertyChanged, no WPF).

- **Test-first, as planned**: `ContainSelfTest` (28 checks — store
  round-trip/upsert/pause/remove/event-ledger, provenance parsing,
  grouping keys, catalog-beats-seed + file detection + JSON state probe
  on temp files, v2 rule-name builders) passed at the source BEFORE the
  move, then again against the shipped assembly after. This is the app's
  safety path (blocks, undo, Restore Internet) — the strictest-proof
  cluster so far.
- Layering now: Contain → Intercept → NetWatch, plus the vendored
  SvchostAnalyzer.Core (SvchostIdentityService decodes instances).
- `BlocksStore.ClassForPath` went internal → public (views label rows
  with it) — the phase-2 accessibility pattern, still a tiny list.
- Five harnesses green: NetWatch, Bits, PyTrace (reassemble), SiteDoctor,
  Contain — all consuming shipped assemblies.

### Phase 4 — delivered: `core/AutoCommand.Forensics.Core`

The question-answering side: ProcessProvenanceService (PID inquiry),
TaskAttributionService, ImageSpawnWatcherService, TraceMetricsService,
the BITS watcher + reason catalog + firewall context, the PyTrace pair,
KnownTaskCatalog, TaskSchedulerService, plus helpers
(AuthenticodeVerifier, MotwUnblocker) and models (BitsJobItem/Stat,
SpawnItem, PyTraceRecord). 17 files; ScheduledTaskItem became the second
DataModels extraction.

- Top of the layering: Forensics → Contain → Intercept → NetWatch
  (TraceMetricsService alone touches all three cores).
- **SpawnItem's two WPF properties (VerdictVisibility/BadgeVisibility)
  became strings** — WPF's VisibilityConverter converts them in bindings,
  so the views changed nothing and the core stays WPF-free. The pattern
  for any future model that carries UI types: rows declare state, the
  view converts.
- First use of `InternalsVisibleTo` (Forensics → BitsTraceSelfTest): the
  BITS harness drives the internal parser/apply seam that a source-copy
  build could see. White-box access without polluting the public API.
- All three source-copying harnesses retired: TaskAttributionSelfTest,
  BitsTraceSelfTest, PyTraceSelfTest now reference the library — every
  harness in the repo runs shipped assemblies now.
- One flaky live-log check made honest: the BITS harness's "Font Download"
  replay asserted a one-time event that aged out of the real BITS
  Operational channel mid-phase; it now tolerates an empty channel window
  (the synthetic parse check carries the logic deterministically).
- Six harnesses green; app Debug + Release clean. Services folder: 36 → 19
  files.

### Phase 5 — delivered: `core/AutoCommand.Sitedoctor.Core`

The "why is this site slow?" pipeline: SiteFingerprintService,
SiteProbeService, SiteVerdictService, DependencyBlockService (+ its Live
half), ServiceOutageMap — 6 files. Layering: Sitedoctor → Contain →
Intercept → NetWatch. The Live half's old blockers (transmissions archive,
block-hit feed) dissolved in phases 2–3, so it moved with the rest.
`InternalsVisibleTo` for SiteDoctorSelfTest's live per-edge probe section
(phase-4 pattern). Six harnesses green; app Debug + Release clean;
Services folder: 19 → 13 files.

### Phase 6 — delivered: `core/AutoCommand.Agent.Core` + the headless face

The Be My Guest stack: AgentBridgeService (MCP listener), AgentToolRegistry
(read-only-by-default tools over every core), McpClientRegistration,
TraceProjectStore + TraceProject — namespaces unchanged.

- **The UI seam inverted through `Sdk\IAgentInteraction`**: the bridge asks
  per-action confirmation through the interface; the WPF shell installs
  `WpfAgentInteraction` (the AgentConfirmWindow dialog) at startup. The
  whole UI coupling of the agent stack was exactly one dialog construction
  plus two view-static helpers — both rehomed first
  (ProcessProvenanceService.BuildReportText, SvchostTraceClassifier).
- **`AgentHost`**: a tiny console face over the same core — headless AI
  sessions no longer boot the WPF application. No interaction installed,
  so mutating tools stay denied-by-default; single-instance guard shared
  with the app. Shipped in the zip under AgentHost\ (framework-dependent).
- ProxyInstallSelfTest consumes the library (InternalsVisibleTo for the
  internal install overload) — the LAST source-copying harness retired;
  all seven harnesses now run shipped assemblies.
- Services folder: 13 → 10 files. The exe now holds views, prefs, and
  wiring; every engine lives in a core.

### The rewrite's structural work is complete

#### Iteration: deferred items resolved (2026-10-27)

- **View conversions — CLOSED, not deferred**: a fresh survey found the
  phase-0 concern had already collapsed. TransmissionsView — flagged as the
  highest-payoff conversion — is already event-driven incremental
  (ObservableCollection adds with a row cap; the ItemsSource hits are a
  filter dropdown). Every repeating timer is now visibility-gated, self-
  gating via Loaded/Unloaded, or on the documented do-not-gate list.
  Remaining hand-rolled poll blocks (TracesView's card) are dedup-only,
  convert opportunistically.
- **Upstream sync window 2**: ported `638766c` (RdapService IANA-bootstrap
  + throttle pacing) into the NetWatch core — see PORTING.md.
- **Namespace migration — DECIDED 2026-10-27: the blocker is gone.** The
  keep-deferred decision was premised on the shared namespaces making
  upstream ports merge-free; with the upstream freeze (see PORTING.md),
  that constraint is dead. Migration to `AutoCommand.<Core>.*` proceeds
  per-cluster, as its own changesets, never mixed with behavior changes.
- **Singleton→DI — see the BlocksLedger pilot below (iteration B).**

#### Iteration: the DI pilot — `BlocksLedger` (2026-10-27)

`BlocksStore` — the safety path's undo ledger — is now TWO things in one
file: `BlocksLedger`, an instance class whose storage location is
**constructor-injected** (plus a parameterless constructor using the
default %LOCALAPPDATA% path), and the unchanged static `BlocksStore`
facade forwarding to one default instance. Every app call site is
untouched; `ContainSelfTest` now constructs its own ledger outright —
the old `BlocksStore.DataDirectory = ... // redirect BEFORE first use`
ordering trap is GONE, and a test can hold two ledgers side by side.

This is the template for converting any remaining static surface, in
this order of value: instance core first, constructor-injected
dependencies, static facade for compatibility, tests migrate to owned
instances. The next candidates, only when a real need appears:
`TlsKeylogService.DirOverride` (the last settable global) and
`TraceProjectStore`. A DI container is deliberately NOT introduced —
with single-instance semantics and one composition point per face
(App.OnStartup, AgentHost.Program), a container would add ceremony
without removing any coupling.

#### Deliberately deferred (cosmetic or behavior-adjacent; do NOT block on them)

- Namespace migration to AutoCommand.<Core>.* (mechanical; do per-cluster
  when touching a cluster anyway)
- Singleton → composed-instance cleanup (the .Instance surfaces work and
  are battle-tested; change only when a real need appears)
- Remaining view conversions to RefreshLoop/ListSync (Transmissions,
  SvchostMonitor pipeline) — phase 0 captured the mechanism; convert
  opportunistically
- Upstream sync: port user-facing fixes from the original repo into the
  cores per release window (PORTING.md tracks them)

### (Historical) remaining phases list

Target layout (all plain `net10.0-windows` class libraries, zero WPF
references, same pattern as the existing vendored engines):

```
Sdk/AutoCommand.Sdk              contracts grow: IAgentInteraction, DTOs
core/AutoCommand.NetWatch.Core   ✔ done (phase 1, see above)
core/AutoCommand.Intercept.Core  ✔ done (phase 2, see above)
core/AutoCommand.Contain.Core    firewall, sinkhole, blocks, Defender
core/AutoCommand.Forensics.Core  provenance, attribution, PyTrace, Bits, traces
core/AutoCommand.Sitedoctor.Core  ✔ done (phase 5, see above)
core/AutoCommand.Agent.Core      bridge, tool registry, MCP registration
```

Rules of the road:

- Singleton/static service surfaces become composed instances; keep static
  forwarding shims during migration so views keep compiling, delete per cluster.
- UI coupling inverts through two interfaces in Sdk (`IAgentInteraction`,
  `INotifier`) implemented by the WPF shell.
- Each extraction switches its self-test harness from
  `<Compile Include="..\Services\X.cs" />` source-copying to a single
  `<ProjectReference>` — stronger than byte-identical: it runs the shipped
  assembly.
- Physical file moves (not linked sources) so the csproj `Compile Remove`
  tax does not grow.
- Order: NetWatch first (leaf services, has a self-test), then Forensics,
  Intercept, Contain, Sitedoctor, Agent last (it touches everything).
- A root `Directory.Build.props` centralizes TFM, `$(Version)`, and the
  `10.0.0-preview.4` package pins.
- No MVVM framework adoption, no public renames, no behavior changes: views
  keep their code-behind; the payoff is testability, headless hosting, and
  self-tests as real library consumers.

## Post-rewrite iterations log

- **Debt-register pass (2026-10-27, post-cutover)** — the register from the
  cutover review, worked in sequence: (1) CI gate workflow (`ci.yml` —
  Debug+Release builds plus the strict offline suites on every push;
  SiteDoctor/TaskAttribution run advisory, live channels on a stock
  runner); (2) `.gitattributes` (renormalize changed zero blobs — the
  repo was already LF-normalized; the file locks the invariant); (3) the
  AgentProxy offline stub list now carries all 22 tools and a
  ProxyInstallSelfTest tripwire fails on any drift from
  `AgentToolRegistry.ToolNames`; (4) per-call urgency (ResolveNow's queue
  item carries its own flag — the process-global skip-spacing token is
  gone); (5) PLAN.md + PORTING.md archived to `docs/history/`; (6) the
  namespace migration (the deferred debt whose blocker died with the
  freeze): every core owns its namespace, Contain/Sitedoctor gained the
  explicit NetWatch ProjectReference their transitive lean-in was hiding,
  `AutoCommand.Models` kept deliberately shared; (7) the geo engine split
  out of DnsResolutionService (`DnsResolutionService.Geo.cs`, partial
  class); (8) `TlsKeylogService.DirOverride` removed — the "dead global"
  guess was wrong: PyTraceSelfTest used it three times, so the seam
  migrated properly (TlsKeylogStore instance core with
  constructor-injected dir; PyTraceSelfTest sweeps a temp dir via
  `new TlsKeylogService(dir)` — never real user state) — and
  TraceProjectStore converted to the BlocksLedger pattern
  (`TraceProjectLibrary` instance + static facade). View
  conversions remain closed by the 2026-10-27 re-survey — every repeating
  timer is gated, self-gating, or on the do-not-gate list; convert
  opportunistically only. AgentHost stays framework-dependent by design
  (documented in its shipped README) — decided, not debt.
- **Cutover declared (2026-10-27)** — `dparksports/autocommand-windows`
  frozen at v2026.10.25.4 (PORTING.md records the closure); this tree
  becomes the one AutoCommand. The shipping bar is unchanged: every
  offline self-test suite green against shipped assemblies, app +
  AgentProxy + AgentHost clean in Debug and Release, release notes per
  version. The freeze unblocks the previously-gated namespace migration
  and retires the port-compat constraint everywhere.
- **v0.7.0 / v2026.10.27.7** — first upstream port through the PORTING.md
  window (RdapService IANA-bootstrap + throttle pacing); deferred items
  resolved (view conversions closed by re-survey; namespace migration
  keep-deferred). Followed by the parallel session's containment feature
  (verify_block_containment + Blocks-tab column) landing in the same
  version window.
- **v0.8.0** — BlocksLedger DI pilot (constructor-injected storage, static
  facade unchanged, the DataDirectory ordering trap removed).
- **v0.9.0 / v2026.10.27.9** — containment review follow-ups: Take()
  fails fast on UI threads, QuietTargets rule moved into the engine,
  attempt-conflation limit documented at the source. Contain suite at 48
  checks.
