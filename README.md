# Windows System Monitor (v3.1.0)
**Advanced Real-Time Security Enforcement & System Analytics**

Windows System Monitor is a professional-grade C# WPF application designed for power users and security administrators. It provides deep visibility into system hardware, network traffic, and security events, combined with automated policy enforcement through a robust "Security Enforcer" background engine.

![Dashboard Preview](DeviceMonitorCS/shield-up-screenshot.jpg)

## 🚀 Key Features

### 🛡️ Security & Privacy Enforcement
*   **Real-Time Monitoring**: Direct integration with the Windows Security Event Log to track logons, process creations, and privilege escalations.
*   **Security Enforcer**: A background engine that monitors system state and enforces security policies (e.g., ensuring kernel debug is disabled).
*   **Privacy Guard**: Toggle Windows telemetry, data collection, and diagnostic reporting with a single click.
*   **AI Insights**: Integrated Gemini AI allows you to query specific system events or components to understand potential threats.

### ⌨️ Advanced Device Management
*   **Peripheral Tracking**: Advanced monitoring for Keyboards, Mice, and Monitors.
*   **Auto-Load Logic**: View status and history immediately upon navigation.
*   **Event History**: Detailed timestamps for when devices were **Started** and **Configured**, extracted directly from Kernel-PnP logs.
*   **Status Indicators**: Clear visual distinction between Connected (OK) and Disconnected/Error states.

### 📊 Performance & Analytics
*   **Live Metrics**: High-fidelity monitoring of CPU, RAM, GPU utilization, and Disk I/O.
*   **Network Intelligence**: 
    *   **Active Connections**: Monitor every application currently communicating over the network.
    *   **Muted Traffic**: Dedicated view for restricted or blocked network attempts.
    *   **Adapter Control**: Inspect and manage physical and virtual network adapters.
*   **Firmware Explorer**: Inspect ACPI tables, UEFI secure boot variables, and BIOS metadata.

### 🛠️ System Control Tools
*   **Firewall Manager**: View and toggle inbound/outbound rules with one-click reset capabilities.
*   **Scheduled Tasks**: Full management (Start/Stop/Delete) of Windows Scheduled Tasks.
*   **Cold Boots**: Analyze system boot history, uptime, and crash events.
*   **True Shutdown**: Bypass Fast Startup for a complete system power cycle.
*   **Hibernation**: Toggle hibernation availability to save disk space.
*   **Hosted Network**: Manage Wi-Fi Hotspots and WAN Miniport adapters.

---

## 📦 Installation & Setup

### Requirements
*   **OS**: Windows 10 or 11 (64-bit)
*   **Privileges**: **Administrator rights** are mandatory for system-level monitoring and enforcement.
*   **AI Features**: Create an `apikey.txt` file in the executable directory and paste your Gemini API key inside.

### Quick Start
1.  **Download**: Get the latest release from the [Releases](https://github.com/dparksports/SystemMonitor/releases) page.
2.  **Run**: Launch `DeviceMonitorCS.exe` as Administrator.
3.  **Install (Recommended)**: Go to **Settings** and click **"Install to Scheduled Tasks"** to enable UAC-bypass auto-start at login.

---

## 📜 Release History

### v3.1.0 (Latest)
*   **Cold Boots View**: New view to track boot performance and history.
*   **Power Tools**: Added "True Shutdown" and Hibernation toggle controls.

### v2.8.1
*   **Consolidation**: Merged peripheral tracking into a unified, auto-loading Device Management view.
*   **Reliability**: Implemented XML-based PnP event parsing for 100% accurate device history.

### v2.8.0
*   **Device History**: Added "Last Started" and "Last Configured" columns using hardware event log analysis.

### v2.7.3
*   **Security**: Hardened release by removing bundled API keys and enforcing manual setup via `apikey.txt`.

---

## 🛠️ Building from Source
1.  Clone the repository.
2.  Open in **Visual Studio 2022** and restore NuGet packages.
3.  Build for **Release | win-x64**.

---

## License
Apache License 2.0

---
*Made with ❤️ in California*
