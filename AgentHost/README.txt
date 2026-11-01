AutoCommand AgentHost — headless AI-access session (no window).

Run:  AutoCommand.AgentHost.exe [seconds]
  AutoCommand.AgentHost.exe 300    auto-stops after 5 minutes
  AutoCommand.AgentHost.exe        runs until stdin closes or Ctrl+C

Requires the .NET 10 runtime. The main AutoCommand.exe ships
self-contained and needs nothing installed; this small console tool is
framework-dependent on purpose (scripts boot it in milliseconds). If
Windows reports a missing runtime, install the .NET 10 Runtime from
https://dotnet.microsoft.com/download/dotnet/10.0 — or skip this tool
entirely and use the Be My Guest tab inside AutoCommand instead: same
tools, same rules, inside the window you already have.

Notes:
- Refuses to start when another AutoCommand instance (app or host) is
  already running — one session at a time.
- Mutating tools are permanently denied here: no confirmation dialog
  exists to ask you, so a headless session can answer questions and
  never change anything.
