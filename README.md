# ⚔ Auto Command

**Auto Command** is a high-fidelity Windows system monitoring and security command center. It combines real-time data accuracy with a premium "Aurora" glassmorphism interface, providing a professional workspace for power users to track system health and security telemetry.

[![Download v3.10.4](https://img.shields.io/badge/Download-v3.10.4-00F0FF?style=for-the-badge&logo=windows)](https://github.com/dparksports/SystemMonitor/releases/download/v3.10.4/AutoCommand_v3.10.4.zip)

![Auto Command Dashboard](DeviceMonitorCS/app_dashboard_mockup.png)

## 🚀 Key Features

### 🛡️ Unified Command Panel
The heart of Auto Command, providing one-click management for critical system interfaces often exploited as "Security Holes."
### 🛡️ **Advanced Firewall Management**
- **Lazy Loading**: Firewall rules load on-demand, significantly improving app startup time.
- **Nested Grouping**:
  - **Inbound/Outbound Separation**: Dedicated tabs for rule direction.
  - **Enabled/Disabled Partitioning**: Rules are intelligently separated into "Enabled Groups" and "Disabled Groups".
  - **Smart Expansion**: Active groups (≤ 5 rules) are auto-expanded for quick visibility; larger groups are collapsed.
- **Sorting**: Enabled groups are sorted by rule count (smallest first) to prioritize manageable sets.
- **Column Visibility**: Fixed rendering issues to ensure all rule details (Ports, Protocols, Actions) are clearly visible.

### 📅 **Scheduled Task Manager** (New in v3.10)
- **Tri-List View**: Tasks are organized into three clear tabs:
  - **Running**: Currently executing tasks.
  - **Ready**: Enabled tasks waiting for triggers.
  - **Disabled**: Inactive tasks.
- **Direct Control**: Run and Stop tasks directly from the dashboard.
- **Async Loading**: Task lists load in the background to keep the UI responsive.

### 🔒 **Privacy & Security**
- **Telemetry Blocking**: One-click disabling of Windows telemetry and tracking services.
- **Service Management**: Toggle background services like SysMain, DiagTrack, and more.
- **Ghost Process Detection**: Identify and terminate hidden background processes.

### ⚡ **System Performance**
- **Process Manager**: View and kill running processes with detailed resource usage.
- **Cold Boots**: (Coming Soon) Optimized startup management.

---

## 📥 Installation
1. Download the latest release: [Auto Command v3.10.4](https://github.com/dparksports/SystemMonitor/releases/download/v3.10.4/AutoCommand_v3.10.4.zip)
2. Extract the ZIP file to a preferred location (e.g., `C:\Tools\AutoCommand`).
3. Run `AutoCommand.exe` as Administrator.

---

## 🛠️ Build from Source
Requirements:
- .NET 8.0 SDK
- Windows 10/11

```powershell
git clone https://github.com/dparksports/SystemMonitor.git
cd SystemMonitor/DeviceMonitorCS
dotnet build -c Release
```

---

<p align="center">Made with ❤️ in California</p>
