# Windows System Monitor

[![Download Latest Release](https://img.shields.io/badge/Download-v2.2.9-blue?style=for-the-badge&logo=windows)](https://github.com/dparksports/DeviceMonitor/releases/download/v2.2.9/DeviceMonitorCS.zip)

**Windows System Monitor** is an advanced system utility designed for power users, administrators, and developers. It provides deep visibility into Windows firmware, security events, and network configurations that are often hidden or difficult to access.

![Screenshot](screenshot.png)

## What's New in v2.2

### 🖥️ Firmware Settings & UEFI Explorer
*   **Native Table Access**: Directly queries the Windows Kernel (via P/Invoke) to list all **ACPI**, **SMBIOS**, and **UEFI** firmware tables.
*   **Smart Deciphering**: Automatically translates cryptic table IDs (e.g., `FACP`, `MCFG`, `APIC`) into human-readable descriptions (e.g., "Fixed ACPI Description Table").
*   **Content Viewer**: Right-click any table to view its **Raw Hex Dump** and decoded **ACPI Header** info (Signature, OEM ID, Checksum).
*   **BCD Explorer**: A structured view of the **Boot Configuration Data**, parsing all hidden settings (`emssettings`, `badmemory`, `globalsettings`) with a "View Details" popup for complex entries.

### 🤖 AI Integration
*   **"Ask AI"**: Context-aware AI integration (powered by Google Gemini). Right-click any confusing entry—whether it's a security event, a BCD flag, or a firmware table—to get an instant explanation in plain English.

### 🛡️ Security & PrivacyEnforcer
*   **Security Event Log**: Real-time stream of security events with "Muted Connections" filtering to ignore noise.
*   **Privacy Toggle**: One-click disable for the "Usage Data" (Telemetry) scheduled task.
*   **Kernel Debug Toggle**: Monitoring and control of Kernel Debug settings to prevent unauthorized tampering.

### 📡 Network Manager
*   **Hosted Network**: Legacy controls to Start/Stop/Repair the Windows Hosted Network (SoftAP).
*   **Adapter & Miniport Repair**: Tools to reset and repair WAN Miniports and network adapters when connectivity fails.
*   **Connection Monitor**: Active TCP/UDP connection tracking with process association.

## Installation

1.  **Download**: Get the latest `DeviceMonitorCS.zip` from the [Releases Page](https://github.com/dparksports/DeviceMonitor/releases).
2.  **Extract**: Unzip the contents to a folder (e.g., `C:\Apps\SystemMonitor`).
3.  **Run**: Launch `DeviceMonitorCS.exe` as **Administrator**.
    *   *Note: Admin privileges are required to access UEFI variables and Security Logs.*

## AI Configuration (Optional)
To enable the "Ask AI" features:
1.  Get a free API key from [Google AI Studio](https://aistudio.google.com/).
2.  Create a file named `apikey.txt` in the same folder as the `.exe`.
3.  Paste your API key into the file.

## Requirements

*   **OS**: Windows 10 or Windows 11 (x64).
*   **Runtime**: .NET 10.0 (The release is self-contained, no separate install required).

---
*Built with ❤️ for the Windows Community*
