# Windows System Monitor

A native PowerShell (WPF) application for monitoring system events and managing VPN services on Windows.

## Features

### 1. Device Monitoring
*   **Real-time Tracking**: Instantly view Plug-and-Play (PnP) devices as they are added or removed (e.g., USB drives, Keyboards).
*   **Detailed Info**: Displays Device Name, Type (e.g., HIDClass, USB), and the Initiator (Logged-on User).

### 2. Security Event Monitoring
*   **Logon Tracking**: Monitors the Windows Security Log for Logon (4624) and Logon Failure (4625) events.
*   **Detailed Insights**: Shows the Account Name, Activity Type (Logon/Failure), and Logon Type (Interactive, Service, Network, etc.) to help distinguish between user actions and system noise.
*   **Polling Architecture**: Uses a robust polling mechanism to ensure stability even without initial Admin rights (though Admin is required to see data).

### 3. VPN Service Management
*   **One-Click Toggle**: A Toolbar button to easily enable/disable VPN-related services.
    *   **Enabled**: Sets `RasMan`, `IKEEXT`, `PolicyAgent`, and `RemoteAccess` services to `Manual` startup.
    *   **Disabled**: Stops these services and sets their startup type to `Disabled` for security/performance.

## Usage

### Prerequisites
*   Windows 10/11
*   PowerShell 5.1 (Built-in)

### How to Run

1.  **Download** the application script `DeviceMonitorGUI.ps1`.
2.  Open a PowerShell terminal.
3.  Run the following command:

```powershell
powershell -ExecutionPolicy Bypass -File .\DeviceMonitorGUI.ps1
```

> [!NOTE]
> For Security Event monitoring and VPN Toggling, you must run the application as **Administrator**.
> Right-click the PowerShell icon and select "Run as Administrator" before launching the script.

## Download

You can download the latest version from the [Releases Page](https://github.com/dparksports/DeviceMonitor/releases).

**Direct Download (v1.0.0):**
[DeviceMonitorGUI.ps1](https://github.com/dparksports/DeviceMonitor/releases/download/v1.0.0/DeviceMonitorGUI.ps1)
