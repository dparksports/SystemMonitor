# Windows System Monitor

A native PowerShell (WPF) application for monitoring system events and managing VPN services on Windows.

## Features

### 1. Device Monitoring
*   **Real-time Tracking**: Instantly view Plug-and-Play (PnP) devices as they are added or removed (e.g., USB drives, Keyboards).
*   **Detailed Info**: Displays Device Name, Type (e.g., HIDClass, USB), and the Initiator (Logged-on User).

### 2. Security Event Monitoring
*   **Complete Visibility**: Monitors **ALL** events in the Windows Security Log (not just logons).
*   **Smart Parsing**: Automatically extracts Account Names and Activity Types (e.g., "Special Logon", "Process Creation", "Privilege Usage").
*   **Logon Type decoding**: For logon events, it decodes the numeric type (Interactive, Service, Network, etc.) into human-readable text.

### 3. VPN Service Management
*   **One-Click Toggle**: A Toolbar button to easily enable/disable VPN-related services.
    *   **Enabled**: Sets `RasMan`, `IKEEXT`, `PolicyAgent`, and `RemoteAccess` services to `Manual` startup.
    *   **Disabled**: Stops these services and sets their startup type to `Disabled` for security/performance.

### 4. Advanced Network Toggles
*   **WiFi Direct**: Enable/Disable the "Microsoft Wi-Fi Direct Virtual Adapter" to block or allow ad-hoc wireless connections.
*   **Kernel Debug Network**: Toggle `bcdedit /debug` on or off to control kernel debugging networking capabilities.
*   **Audit Logging**: All toggle actions are recorded in the Device Events list with the Initiator's identity.

### 5. Scheduled Tasks Manager
*   **Dedicated Window**: Click the "Scheduled Tasks" button to open a separate task management window.
*   **View Admin Tasks**: Displays all scheduled tasks running with highest privileges (administrator rights).
*   **Task Management**: Full control over tasks with dedicated buttons:
    *   **Disable**: Prevent tasks from running automatically
    *   **Stop**: Terminate currently running tasks
    *   **Start**: Execute tasks immediately
    *   **Delete**: Permanently remove tasks (with confirmation)
    *   **Refresh**: Update the task list to show current status
*   **Task Details**: View task name, state (Ready, Running, Disabled), action (command being executed), and user account.

## Usage

### Prerequisites
*   Windows 10/11
*   PowerShell 5.1 (Built-in)

### Installation (UAC Bypass)

To run the application as Administrator without constant prompts:

1.  **Download** and extract the release zip `DeviceMonitor.zip`.
2.  Right-click `Install.ps1` and select **"Run with PowerShell"**.
    *   *Note: You will be prompted for Admin rights once during installation.*
3.  The installer will create a desktop shortcut.

**Usage:**
- Double-click the **"Windows System Monitor"** shortcut on your desktop.
- It will open immediately with full privileges and no UAC prompt.

### Quick Run (No Install)
If you prefer not to install, you can still run it manually:
```powershell
powershell -ExecutionPolicy Bypass -File .\DeviceMonitorGUI.ps1
```

> [!NOTE]
> For Security Event monitoring and VPN Toggling, you must run the application as **Administrator**.
> Right-click the PowerShell icon and select "Run as Administrator" before launching the script.

## Download

You can download the latest version from the [Releases Page](https://github.com/dparksports/DeviceMonitor/releases).

**Direct Download (v1.2.0):**
[DeviceMonitor.zip](https://github.com/dparksports/DeviceMonitor/releases/download/v1.2.0/DeviceMonitor.zip)
