using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AutoCommand.Views
{
    /// <summary>
    /// Traffic Peek — "what does this connection actually carry?", opened
    /// from the Process Monitor row context menu. While open it holds the
    /// shared TCP stream capture (FlowDossierService AddRef/Release), shows
    /// every finalized flow to the peered IP with the TLS decryptor's honest
    /// verdict, and reads the Windows Update / BITS / Delivery Optimization
    /// event channels for the same time window — the plaintext answer for
    /// services whose TLS can never be decrypted.
    ///
    /// Takes identity values (not the live row) so the window survives the
    /// process exiting — the ProcessTraceWindow pattern.
    /// </summary>
    public partial class ConnectionPeekWindow : Window
    {
        private const int MaxFlowsShown = 200;

        private readonly string _remoteIp;
        private readonly string _processName;
        private readonly int _pid;
        private readonly string _hostedServices;
        private readonly string _hostname;
        private readonly DateTime _openedUtc = DateTime.UtcNow;

        private FlowDossierService _dossier;
        private readonly ObservableCollection<FlowDossierEntry> _flows = new();
        private readonly ObservableCollection<ServiceEventLine> _events = new();
        private readonly DispatcherTimer _statusTimer;
        private DateTime _lastEventRefreshUtc = DateTime.MinValue;
        private bool _busyRefreshingEvents;
        private bool _closed;

        public ConnectionPeekWindow(string remoteIp, string processName, int pid, string hostedServices, string hostname)
        {
            _remoteIp = remoteIp;
            _processName = processName;
            _pid = pid;
            _hostedServices = hostedServices;
            _hostname = hostname;
            InitializeComponent();

            Title = $"Traffic Peek — {remoteIp}";
            IdentIp.Text = remoteIp;
            IdentProcess.Text = BuildProcessLabel();
            IdentServices.Text = string.IsNullOrEmpty(hostedServices) ? "—" : hostedServices;
            IdentHost.Text = string.IsNullOrEmpty(hostname) ? "(not resolved)" : hostname;

            FlowList.ItemsSource = _flows;
            EventList.ItemsSource = _events;
            UpdateKeylogChip();

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _statusTimer.Tick += (_, _) => UpdateStatusStrip();

            Loaded += (_, _) => { _statusTimer.Start(); StartPeek(); };
            Closed += (_, _) => { _closed = true; _statusTimer.Stop(); StopPeek(); };
        }

        private string BuildProcessLabel() =>
            (_processName ?? "svchost.exe") + (_pid > 0 ? $" · PID {_pid}" : string.Empty);

        // ── lifecycle ────────────────────────────────────────────────────

        private void StartPeek()
        {
            _dossier = new FlowDossierService(_remoteIp);
            _dossier.OnEntry += entry =>
                Dispatcher.BeginInvoke(new Action(() => AddFlow(entry)));
            // Non-fatal by contract (Sysmon absent → no PID attribution);
            // shown as a note, never a stop.
            _dossier.OnError += msg =>
                Dispatcher.BeginInvoke(new Action(() =>
                    SetWarning("PID attribution note: " + msg)));
            _dossier.Start();
            _ = RefreshEventsAsync();
            UpdateStatusStrip();
        }

        private void StopPeek()
        {
            try { _dossier?.Stop(); } catch { /* the capture outliving us is fine */ }
            _dossier = null;
        }

        // ── flows ────────────────────────────────────────────────────────

        private void AddFlow(FlowDossierEntry entry)
        {
            _flows.Insert(0, entry);
            while (_flows.Count > MaxFlowsShown)
                _flows.RemoveAt(_flows.Count - 1);
            UpdateStatusStrip();

            // The service-activity window is anchored to the earliest flow
            // we saw; refresh at most every 15 s.
            if ((DateTime.UtcNow - _lastEventRefreshUtc).TotalSeconds >= 15)
                _ = RefreshEventsAsync();
        }

        private void FlowList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RenderDetail(FlowList.SelectedItem as FlowDossierEntry);
        }

        private void RenderDetail(FlowDossierEntry entry)
        {
            if (entry == null)
            {
                DetailWire.Text = "No flow selected.";
                DetailTls.Text = DetailSni.Text = DetailApp.Text = string.Empty;
                StatusBanner.Visibility = Visibility.Collapsed;
                PlaintextSection.Visibility = Visibility.Collapsed;
                return;
            }

            var flow = entry.Flow;
            string flags =
                (flow.FinBoth ? " · FIN both" : "") +
                (flow.Reset ? " · RST" : "") +
                (flow.Truncated ? " · capture truncated" : "");
            DetailWire.Text = $"{entry.RemoteEndpoint}   ←   {entry.LocalEndpoint}   ·   {entry.UpDisplay} up · {entry.DownDisplay} down{flags}";

            string tls = entry.Decrypt.TlsVersion ?? "TLS version unknown";
            if (!string.IsNullOrEmpty(entry.Decrypt.Alpn)) tls += $" · ALPN {entry.Decrypt.Alpn}";
            if (!string.IsNullOrEmpty(entry.Decrypt.CipherSuite)) tls += $" · {entry.Decrypt.CipherSuite}";
            DetailTls.Text = tls;
            DetailSni.Text = "SNI: " + entry.SniDisplay;
            DetailApp.Text = entry.Decrypt.Success
                ? $"plaintext: {entry.Decrypt.ClientAppBytes} B up · {entry.Decrypt.ServerAppBytes} B down"
                : string.Empty;

            // Verbatim decryptor verdict, color-coded: green = decrypted,
            // amber = honest "can't and here's why", red = unexpected.
            StatusBanner.Visibility = Visibility.Visible;
            StatusBannerText.Text = entry.Decrypt.Status ?? "(no status)";
            string status = entry.Status ?? "";
            (string bg, string fg) =
                status.StartsWith("decrypted", StringComparison.Ordinal)      ? ("#14351F", "#7ED8A0") :
                status.StartsWith("encrypted", StringComparison.Ordinal)      ? ("#3A2E10", "#FBBF24") :
                status.StartsWith("not TLS", StringComparison.Ordinal)        ? ("#3A2E10", "#FBBF24") :
                                                                                ("#3A1520", "#F2A0B4");
            StatusBanner.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bg));
            StatusBannerText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fg));

            bool showPlaintext = entry.Decrypt.Success;
            PlaintextSection.Visibility = showPlaintext ? Visibility.Visible : Visibility.Collapsed;
            if (!showPlaintext) return;

            DetailHttp.Text = string.IsNullOrEmpty(entry.Decrypt.HttpSummary)
                ? null
                : "HTTP/session: " + entry.Decrypt.HttpSummary;

            var sb = new StringBuilder();
            var hex = new StringBuilder();
            int written = 0;
            foreach (var rec in entry.Decrypt.Records)
            {
                if (rec == null || !rec.Decrypted || rec.Data == null || rec.Data.Length == 0) continue;
                sb.AppendLine($"── {rec.ContentType} · {(rec.FromClient ? "client → server" : "server → client")} · {rec.Data.Length} B ──");
                AppendPrintable(sb, rec.Data, 32 * 1024 - sb.Length);
                sb.AppendLine();
                if (hex.Length < 8 * 1024) AppendHex(hex, rec.Data, 8 * 1024 - hex.Length);
                written += rec.Data.Length;
                if (written >= 256 * 1024) { sb.AppendLine("… (further records omitted)"); break; }
            }
            PlaintextBox.Text = sb.Length == 0 ? "(no decrypted application data in this capture)" : sb.ToString();
            HexBox.Text = hex.ToString();
        }

        /// <summary>Printable preview: control bytes become '·', the usual
        /// whitespace survives. A plaintext pane is for reading, not dumping.</summary>
        private static void AppendPrintable(StringBuilder sb, byte[] data, int budget)
        {
            int max = Math.Min(data.Length, Math.Max(0, budget));
            for (int i = 0; i < max; i++)
            {
                byte b = data[i];
                char c = b switch
                {
                    (byte)'\r' or (byte)'\n' or (byte)'\t' => (char)b,
                    < 0x20 or >= 0x7F => '·',
                    _ => (char)b
                };
                sb.Append(c);
            }
            if (max < data.Length) sb.Append($"\n… ({data.Length - max} more bytes)");
        }

        private static void AppendHex(StringBuilder sb, byte[] data, int budgetChars)
        {
            const int perLine = 16;
            for (int offset = 0; offset < data.Length && sb.Length < budgetChars; offset += perLine)
            {
                sb.Append($"{offset:X6}  ");
                for (int i = 0; i < perLine; i++)
                {
                    if (offset + i < data.Length) sb.Append($"{data[offset + i]:X2} ");
                    else sb.Append("   ");
                    if (i == 7) sb.Append(' ');
                }
                sb.AppendLine();
            }
        }

        // ── status strip ─────────────────────────────────────────────────

        private void UpdateStatusStrip()
        {
            if (_dossier == null)
            {
                CaptureStatusText.Text = "Capture stopped.";
                return;
            }
            var capture = TcpStreamCaptureService.Shared;
            CaptureStatusText.Text =
                $"Capture running · {_dossier.EntryCount} peered flow(s) shown · " +
                $"{capture.OutboundPackets} out / {capture.InboundPackets} in packets this session · " +
                "flows appear when a conversation closes (or ~10 s after it goes quiet).";

            // Some NIC/driver combos deliver only outbound packets to a
            // receive-all socket — say so instead of letting a zero reply
            // count read as "the server never answered".
            if (!capture.InboundSeen && capture.OutboundPackets > 0)
                SetWarning("Only outbound packets observed on this NIC — server replies are not being captured " +
                           "(driver limitation). Metadata comes from the outgoing direction only.");
        }

        private void SetWarning(string message)
        {
            CaptureWarnText.Text = "⚠ " + message;
            CaptureWarnText.Visibility = Visibility.Visible;
        }

        // ── service activity ─────────────────────────────────────────────

        private void RefreshEventsBtn_Click(object sender, RoutedEventArgs e) => _ = RefreshEventsAsync();

        private async Task RefreshEventsAsync()
        {
            if (_busyRefreshingEvents || _closed) return;
            _busyRefreshingEvents = true;
            _lastEventRefreshUtc = DateTime.UtcNow;
            try
            {
                // Window: the earliest captured flow − 2 min → now, or the
                // last 15 minutes while nothing has arrived yet.
                DateTime start = _openedUtc;
                if (_flows.Count > 0)
                    start = _flows.Min(f => f.Flow.StartedUtc == default(DateTime) ? _openedUtc : f.Flow.StartedUtc);
                start = start.ToUniversalTime().AddMinutes(-2);
                var lines = await Task.Run(() => ServiceLogProbe.QueryEvents(start, DateTime.UtcNow));
                if (_closed) return;
                _events.Clear();
                foreach (var line in lines) _events.Add(line);
            }
            catch (Exception ex)
            {
                if (!_closed) SetWarning("Service activity lookup failed: " + ex.Message);
            }
            finally
            {
                _busyRefreshingEvents = false;
            }
        }

        // ── footer actions ───────────────────────────────────────────────

        private void UpdateKeylogChip() =>
            KeylogChip.Content = TlsKeylogService.KeylogEnabled
                ? "🔑 Session-key logging: ON"
                : "🔑 Session-key logging: OFF";

        private void KeylogChip_Click(object sender, RoutedEventArgs e)
        {
            bool enabling = !TlsKeylogService.KeylogEnabled;
            var confirm = MessageBox.Show(
                enabling
                    ? "Re-enable TLS session-key logging?\n\nOnly apps that opt in (instrumented python, SSLKEYLOGFILE-aware browsers) ever write keys there. Windows services never do."
                    : "Disable TLS session-key logging?\n\nThe keylog file is deleted and no future session keys are recorded. Flows captured from now on will report 'keylogging is disabled'.",
                "Traffic Peek", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                if (enabling) TlsKeylogService.EnableKeylogging();
                else TlsKeylogService.DisableKeylogging();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Keylog toggle failed: " + ex.Message, "Traffic Peek",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            UpdateKeylogChip();
        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(BuildReportText());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Copy failed: " + ex.Message, "Traffic Peek",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string BuildReportText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== TRAFFIC PEEK: {_remoteIp} ===");
            sb.AppendLine($"process: {BuildProcessLabel()}   services: {_hostedServices ?? "—"}   host: {_hostname ?? "(not resolved)"}");
            sb.AppendLine($"window: {_openedUtc:yyyy-MM-dd HH:mm:ss} → {DateTime.Now:HH:mm:ss} (local)");
            sb.AppendLine();
            sb.AppendLine($"--- FLOWS ({_flows.Count}) ---");
            foreach (var f in _flows)
            {
                sb.AppendLine($"{f.TimeDisplay}  {f.RemoteEndpoint,-22} {f.UpDisplay,9} ↑ {f.DownDisplay,9} ↓  {f.Status,-24} SNI={f.SniDisplay}  {f.ProcessDisplay}");
                if (!string.IsNullOrEmpty(f.Decrypt?.Status)) sb.AppendLine($"          └ {f.Decrypt.Status}");
            }
            sb.AppendLine();
            sb.AppendLine($"--- SERVICE ACTIVITY ({_events.Count}) ---");
            foreach (var ev in _events)
                sb.AppendLine($"{ev.TimeDisplay}  [{ev.ChannelDisplay} id {ev.EventId}]  {ev.Message}");
            return sb.ToString();
        }
    }
}
