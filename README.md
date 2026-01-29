# 🛡️ Auto Command

**Auto Command** is a next-generation system security monitor for Windows, bringing enterprise-grade forensics to a premium, consumer-friendly interface. It provides active protection, deep firmware inspection, and automated vulnerability remediation.

[![Download v3.14.3](https://img.shields.io/badge/Download-v3.14.3-0078D7?style=for-the-badge&logo=windows)](https://github.com/dparksports/SystemMonitor/releases/tag/v3.14.3)

![DBX Safety Dashboard](assets/dashboard_v3.14.0.png)

## 🚀 Key Capabilities

### 🔑 **New in v3.14: DBX Safety Check**
A specialized tool for pre-flight bootloader validation and remediation. Use the **Key Icon** in the sidebar to access:

*   **Integrity Verification**: Compares your running bootloader hash against the UEFI "Forbidden Signature Database" (DBX).
*   **Dual Remediation Strategy**:
    *   **Direct Repair**: Instantly reinstall a clean Microsoft Bootloader (`bcdboot`) to overwrite potential bootkits.
    *   **Recovery Handoff**: One-click access to the Windows Recovery Environment (`ms-settings:recovery`) for manual troubleshooting.
*   **Testing Mode**: A debug toggle in Settings allows you to inspect the remediation UI even on safe systems.

### 🔒 **Advanced Firmware Protection**
Directly interface with your motherboard's NVRAM to detect and mitigate stealthy threats.
*   **Secure Boot Inspection**: Visualize `db` (Allowed) and `dbx` (Forbidden) signature databases.
*   **Vulnerability Detection**: Identifies if your system is vulnerable to known bootkits like BlackLotus (CVE-2022-21894).

### ⚡ **Interactive Security Dashboard**
*   **Live Threat Status**: Instant, color-coded health status (e.g., "Firmware Risk", "System Safe").
*   **Deep Drill-Down**: Navigate instantly from status cards to deep-dive inspection tools.
*   **Activity Feed**: Unified timeline of security events, firewall changes, and device connections.

### 🛡️ **Network & Firewall Dominance**
*   **Clarified Rules**: Groups and organizes firewall rules to spot anomalies.
*   **Drift Defense**: Alerts you if a program or update silently modifies your firewall configuration.
*   **Traffic Analysis**: Function to monitor active connections in real-time.

---

## 📥 Getting Started

1.  **Download**: Get the latest `AutoCommand.zip` from the [Releases Page](https://github.com/dparksports/SystemMonitor/releases).
2.  **Extract**: Unzip to a permanent location (e.g., `C:\Program Files\AutoCommand`).
3.  **Run**: Launch `AutoCommand.exe` as Administrator (required for hardware acccess).

---

## 🛠️ Build it Yourself

**Requirements**: Windows 10/11, .NET 8.0 SDK

```powershell
# Clone the repository
git clone https://github.com/dparksports/SystemMonitor.git

# Navigate to the project
cd SystemMonitor/DeviceMonitorCS

# Build Release
dotnet build -c Release
```

---

<p align="center">Made with ❤️ for the Windows enthusiast community.</p>
