# ⚔ Auto Command

**Auto Command** is a high-fidelity system command center for Windows. It bridges the gap between raw system forensics and premium user experience, offering a "glassmorphism" interface to monitor, secure, and optimize your machine.

[![Download v3.11.1](https://img.shields.io/badge/Download-v3.11.1-00F0FF?style=for-the-badge&logo=windows)](https://github.com/dparksports/SystemMonitor/releases/tag/v3.11.1)

![Auto Command Dashboard](DeviceMonitorCS/app_dashboard_mockup.png)

## ✨ Key Features

### 🔒 Secure Boot Forensics (New in v3.11)
Directly inspect the UEFI Secure Boot databases hidden in NVRAM.
- **Allowed Signatures (`db`)**: View all certificates authorized to boot on your system.
- **Revocation List (`dbx`)**: Search forbidden hashes (including known threats like BlackLotus).
- **Change Detection**: Real-time alerts if your Secure Boot policy is modified by an attacker or update.

### 🛡️ Firewall Commander
Stop wrestling with the legacy Windows Firewall console.
- **Smart Grouping**: Rules are automatically grouped and sorted by relevance.
- **Drift Detection**: Alerts you when rules change unexpectedly.
- **One-Click Hardening**: Block telemetry and unnecessary outbound traffic instantly.

### ⚡ System Intelligence
- **Process Manager**: Kill hidden background processes and ghost tasks.
- **Service Control**: Toggle Windows services (SysMain, DiagTrack) without navigating `services.msc`.
- **Connections**: Monitor active network connections and identify "phone home" behavior.

### 🎨 Premium Experience
- **Aurora UI**: A modern, translucent interface that feels at home on Windows 11.
- **Performance First**: Lazy loading and async operations ensure the UI never freezes, even when parsing thousands of system events.

---

## 📥 Installation

1. **Download**: Grab the latest release from the [Releases Page](https://github.com/dparksports/SystemMonitor/releases).
2. **Extract**: Unzip the archive to a permanent location (e.g., `C:\Tools\AutoCommand`).
3. **Run**: Launch `AutoCommand.exe` as Administrator.

> **Note**: Administrator privileges are required to access low-level system details like UEFI variables and Firewall rules.

---

## 🛠️ Build from Source

**Prerequisites**:
- Windows 10/11
- .NET 8.0 SDK

```powershell
# Clone the repository
git clone https://github.com/dparksports/SystemMonitor.git

# Navigate to the project
cd SystemMonitor/DeviceMonitorCS

# Build Release
dotnet build -c Release
```

---

<p align="center">Made with ❤️ in California</p>
