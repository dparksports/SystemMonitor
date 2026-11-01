using System;
using AutoCommand.Agent.Core;
using AutoCommand.Contain.Core;
using AutoCommand.Forensics.Core;
using AutoCommand.Intercept.Core;
using AutoCommand.NetWatch.Core;
using AutoCommand.Sitedoctor.Core;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AutoCommand.Services;

namespace AutoCommand.Views
{
    /// <summary>
    /// The Investigate tab: one composite read-only investigation per run —
    /// provenance + traffic profile + block hits, joined by
    /// ProcessInvestigationService into a verdict and a plain-text report.
    /// The service does all the work; this view only collects input, keeps
    /// the UI responsive during the archive scan, and colors the verdict.
    /// </summary>
    public partial class InvestigateView : UserControl
    {
        private string _lastReport;
        private string _lastFileStem;

        public InvestigateView()
        {
            InitializeComponent();
        }

        private async void RunBtn_Click(object sender, RoutedEventArgs e)
        {
            string subject = (SubjectBox.Text ?? "").Trim();
            if (subject.Length == 0)
            {
                ProgressText.Text = "Type a process name (e.g. Reolink.exe) or a PID first.";
                return;
            }

            int? pid = null;
            string name = subject;
            if (int.TryParse(subject, out int parsedPid))
            {
                pid = parsedPid;
                name = null;
            }

            int hours = HoursBox.SelectedIndex switch { 1 => 12, 2 => 24, _ => 6 };

            RunBtn.IsEnabled = false;
            VerdictPanel.Visibility = Visibility.Collapsed;
            ReportBox.Visibility = Visibility.Collapsed;
            CopyBtn.Visibility = Visibility.Collapsed;
            SaveBtn.Visibility = Visibility.Collapsed;
            ProgressText.Text = "Reading recorded archives…";

            try
            {
                var inv = await Task.Run(() => ProcessInvestigationService.Investigate(name, pid, hours));
                ShowVerdict(inv);
                _lastReport = ProcessInvestigationService.BuildReportText(inv);
                _lastFileStem = SanitizeStem(inv.ImageName ?? inv.Requested ?? "process");
                ReportBox.Text = _lastReport;
                ReportBox.Visibility = Visibility.Visible;
                CopyBtn.Visibility = Visibility.Visible;
                SaveBtn.Visibility = Visibility.Visible;
                ProgressText.Text = $"Done — {inv.Evidence.Count} evidence line(s), window {hours} h.";
            }
            catch (Exception ex)
            {
                ProgressText.Text = $"Investigation failed: {ex.Message}";
            }
            finally
            {
                RunBtn.IsEnabled = true;
            }
        }

        private void ShowVerdict(ProcessInvestigationService.Investigation inv)
        {
            VerdictHeadline.Text = inv.VerdictHeadline;
            VerdictDetail.Text = inv.VerdictDetail;

            (Brush bg, Brush border, Brush fg) = inv.Kind switch
            {
                ProcessInvestigationService.VerdictKind.Blocked =>
                    (Hex("#3A1414"), Hex("#E5534B"), Hex("#FF8A80")),
                ProcessInvestigationService.VerdictKind.OutboundAttempts =>
                    (Hex("#3A2E14"), Hex("#E5A631"), Hex("#E5A631")),
                ProcessInvestigationService.VerdictKind.InternalOnly or
                ProcessInvestigationService.VerdictKind.Quiet =>
                    (Hex("#122A1A"), Hex("#3FB950"), Hex("#7EE2A8")),
                ProcessInvestigationService.VerdictKind.Mixed =>
                    (Hex("#14243A"), Hex("#4A9EFF"), Hex("#7FB8FF")),
                _ => (Hex("#23262B"), Hex("#6E7681"), Hex("#C9D1D9")),
            };
            VerdictPanel.Background = bg;
            VerdictPanel.BorderBrush = border;
            VerdictHeadline.Foreground = fg;
            VerdictPanel.Visibility = Visibility.Visible;
        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastReport)) return;
            try
            {
                Clipboard.SetText(_lastReport);
                ProgressText.Text = "Report copied to the clipboard.";
            }
            catch (Exception ex)
            {
                ProgressText.Text = $"Copy failed: {ex.Message}";
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastReport)) return;
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AutoCommand", "reports");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir,
                    $"{_lastFileStem}-{DateTime.Now:yyyyMMdd-HHmmss}.md");
                File.WriteAllText(path, _lastReport);
                ProgressText.Text = "Saved: " + path;
            }
            catch (Exception ex)
            {
                ProgressText.Text = $"Save failed: {ex.Message}";
            }
        }

        private static string SanitizeStem(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s.Length == 0 ? "process" : s;
        }

        private static Brush Hex(string s)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s));
            b.Freeze();
            return b;
        }
    }
}
