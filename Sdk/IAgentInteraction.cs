using System;

namespace AutoCommand.Sdk
{
    /// <summary>
    /// The UI seam the agent bridge asks per-action confirmation through
    /// (AutoCommand's "nothing is touched without your yes" guarantee for
    /// AI-assistant sessions). Implemented by the WPF shell (the
    /// AgentConfirmWindow dialog, marshaled onto the dispatcher); headless
    /// hosts simply do not install one — the bridge then denies mutating
    /// tools with the plain-language note, exactly as it always did when no
    /// UI was present.
    ///
    /// Implementations handle their own marshaling to their UI thread; the
    /// call blocks until the user answers. Timeout handling and the
    /// "always for this session" grant bookkeeping stay in the bridge.
    /// </summary>
    public interface IAgentInteraction
    {
        (bool Allowed, bool AlwaysForTool, string Note) RequestConfirmation(string tool, string detail);
    }
}
