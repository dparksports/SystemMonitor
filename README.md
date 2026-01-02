# Windows System Monitor (v2.4.0)
A robust C# WPF application for advanced real-time system monitoring and security enforcement.

**New in v2.4.0:**
- **Privacy Dashboard Redesign:** Modern UI with toggle switches and **Tamper Protection** status monitoring.
- **Native Device Monitoring:** Instant USB/Device detection using Windows API (`RegisterDeviceNotification`) - no more lag!
- **Firewall Config Drift:** Detects unauthorized firewall changes and prompts to restore your saved settings instantly.
- **AI-Powered Insights:** Integrated Gemini AI for contextual explanation of system components and firewall rules.
A comprehensive system utility for monitoring, managing, and securing your Windows environment. This application offers advanced control over network adapters, scheduled tasks, firmware settings, and system privacy, all within a modern, dark-themed UI.

![Dashboard Preview](DeviceMonitorCS/shield-up-screenshot.jpg)

## [Download Validated Release (v2.5.2.zip)](https://github.com/dparksports/DeviceMonitor/releases/download/v2.5.2/DeviceMonitorCS_v2.5.2.zip)
*Verified Clean - Windows Security Compliant*

## Features

### 🖥️ Dashboard & System Control
- **Quick Toggles**: Instantly enable/disable critical system features:
    - **VPN Services**: Toggle background VPN services.
    - **WiFi Direct**: Manage WiFi Direct capabilities.
    - **Kernel Debug**: Enable/disable kernel debugging networking.
    - **Usage Data**: Toggle Windows telemetry and data collection.
- **Event Log**: Real-time monitoring of system and security events.

### 📊 System Performance
- **Real-time Monitoring**: Visualize system resource usage.
    - **CPU & RAM**: Live usage stats with graphical bars.
    - **Network**: Real-time upload and download speeds.
    - **GPU**: Detailed GPU memory and utilization stats.
    - **Disk**: Read/Write speeds for all active drives.

### 🔒 Privacy Manager
- **Shield Up Status**: Prominent "Shield Up" visual indicator on the dashboard confirming active privacy protection.
- **Telemetry Control**: Deep control over Windows diagnostic data sent to Microsoft.
- **AI Integration**: Ask Gemini AI about specific system components (e.g., "What does UsageDataReceiver do?") directly from the app.

### 📡 Network Management
- **Hosted Network Manager**: Create, stop, and manage Windows Hosted Networks (Hotspots).
- **Network Adapters**: View all physical and virtual network adapters, with options to disable/enable them.
- **WAN Miniports**: robust management of WAN Miniport drivers.
- **Connections Monitor**:
    - **Active Connections**: See what apps are connecting to the internet.
    - **Muted Connections**: View blocked or muted restricted traffic.

### ⚙️ System Tools
- **Scheduled Tasks Manager**: View, stop, start, and delete Windows Scheduled Tasks.
- **Firmware Explorer**:
    - **ACPI Tables**: Dump and inspect system ACPI tables.
    - **UEFI Variables**: View secure boot and other UEFI firmware variables.
    - **BIOS Info**: Detailed BIOS version and release data.

### 🛡️ Firewall Settings
- **Rule Management**: View all Inbound and Outbound firewall rules.
- **Toggle Rules**: Enable or disable specific rules instantly.
- **Reset to Defaults**: One-click reset for Windows Firewall configuration.

### 🛠️ Settings & Installation
- **Run at Startup**: Easily install the application as a highly-privileged Scheduled Task to ensure it runs automatically at logon without UAC prompts.
- **Security Enforcer**: Configurable background scanning interval for enforcing security policies.

## Installation

### Method 1: Portable Run
1. Download the latest release.
2. Unzip the archive.
3. Run `DeviceMonitorCS.exe` as Administrator.

### Method 2: Auto-Start Installation
1. Run the application as Administrator.
2. Navigate to **Settings**.
3. Under "Startup Behavior", click **"Install to Scheduled Tasks"**.
4. The app will now start automatically when you log in.

## Release

- **Version:** v2.3.2
- **Release Date:** 2026-01-01
- Download: [DeviceMonitorCS v2.3.2](https://github.com/dparksports/SystemMonitor/releases/tag/v2.3.2)


## Requirements
- **OS**: Windows 10/11 (64-bit recommended)
- **Runtime**: .NET Framework 4.8 or .NET 6+ (depending on build variant)
- **Privileges**: Administrator rights are required for most features (managing drivers, services, and tasks).


## Building from Source

1. Clone the repository.
2. Open the solution in Visual Studio 2022.
3. Restore NuGet packages.
4. Build the solution (Release mode recommended).


## License
Apache License 2.0

---
*Made with ❤️ in California*
