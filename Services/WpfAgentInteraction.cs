using System;
using System.Windows;
using System.Windows.Threading;

using AutoCommand.Agent.Core;
using AutoCommand.Contain.Core;
using AutoCommand.Forensics.Core;
using AutoCommand.Intercept.Core;
using AutoCommand.NetWatch.Core;
using AutoCommand.Sitedoctor.Core;

namespace AutoCommand.Services
{
    /// <summary>
    /// The WPF implementation of the agent bridge's confirmation seam
    /// (Sdk\IAgentInteraction): marshals to the dispatcher and shows the
    /// AgentConfirmWindow dialog, Topmost, owned by the main window.
    /// Installed by App.OnStartup; headless hosts never install one, which
    /// keeps their mutating tools denied-by-default with no dialog anywhere.
    /// Extracted from AgentBridgeService in the phase 6 Agent-core move.
    /// </summary>
    public sealed class WpfAgentInteraction : Sdk.IAgentInteraction
    {
        public (bool Allowed, bool AlwaysForTool, string Note) RequestConfirmation(string tool, string detail)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                return (false, false, "no UI to confirm with");

            var tcs = new TaskCompletionSource<(bool, bool, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
            dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var win = new Views.AgentConfirmWindow(tool, detail)
                    {
                        Owner = Application.Current?.MainWindow,
                        Topmost = true,
                    };
                    win.ShowDialog();
                    tcs.TrySetResult((win.Allowed, win.AlwaysForTool, win.Allowed ? "allowed by user" : "denied by user"));
                }
                catch (Exception ex) { tcs.TrySetResult((false, false, "confirm dialog failed: " + ex.Message)); }
            });
            return tcs.Task.GetAwaiter().GetResult();
        }
    }
}
