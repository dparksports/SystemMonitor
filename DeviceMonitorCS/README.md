# Windows System Monitor

[![Download Latest Release](https://img.shields.io/badge/Download-v2.1.3-blue?style=for-the-badge&logo=windows)](https://github.com/dparksports/SystemMonitor/releases/download/v2.1.3/DeviceMonitorCS_v2.1.3.zip)

**Windows System Monitor** is a powerful, C#-based WPF application designed to give you deep visibility and control over your Windows environment. It goes beyond standard task managers by providing real-time hardware tracking, advanced network management, security event logging, and privacy controls.

![Screenshot](screenshot.png)

## Key Features

### 🛡️ Security & Privacy
*   **Privacy Settings (NEW)**: Toggle the "Usage Data" (telemetry) scheduled task with a single click.
*   **AI Insights (NEW)**: Integrated **Gemini AI** explains complex system components and potential threats in plain English.
*   **Security Enforcer**: Real-time monitoring of security event logs with automated threat blocking and analysis.

### 📡 Network Management
*   **Connection Monitor**: View all active TCP/UDP connections, including process names, remote locations, and data transfer.
*   **Muted Connections**: Filter out known safe connections to focus on suspicious activity.
*   **Hosted Network Manager**: Manage the legacy Windows Hosted Network feature.
*   **Wifi Direct & VPN Toggles**: Quickly enable or disable network features that can expose your device.

### ⚙️ System Control
*   **Hardware Events**: Monitor PnP device insertion (USB drives, etc.) in real-time.
*   **Scheduled Tasks**: View and manage Windows Scheduled Tasks.
*   **Kernel Debug Toggle**: Easily enable/disable kernel debugging to prevent unauthorized tampering.

## Installation

1.  **Download**: Click the download button above to get the latest `DeviceMonitorCS_v2.1.3.zip`.
2.  **Extract**: Unzip the contents to a folder of your choice (e.g., `C:\Apps\SystemMonitor`).
3.  **Run**: Double-click `DeviceMonitorCS.exe`.
    *   *Note: Runs best as Administrator to access all system features.*
4.  **AI Setup**: To enable AI features, create a file named `apikey.txt` in the app folder and paste your Gemini API key inside.

## Usage

*   **Dashboard**: Overview of recent security and hardware events.
*   **System Performance**: Real-time graphs for CPU, Memory, and Network usage.
*   **Privacy Settings**: Control Windows telemetry and ask AI about system components.
*   **Quick Actions**: Sidebar toggles for VPN, WiFi Direct, Kernel Debug, and Usage Data.

## Requirements

*   Windows 10 or Windows 11
*   .NET 10.0 Runtime (included in self-contained builds or required separately)
*   Administrator privileges (recommended)

---
*Built with ❤️ by the Windows System Monitor Team*
