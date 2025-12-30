# Windows System Monitor (C# / WPF)

A native .NET 6+ WPF application for monitoring system events and managing VPN services on Windows. Ported from the original PowerShell script for better performance and maintainability.

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

### 6. Network Adapters Manager
*   **Dedicated Window**: Click the "Network Adapters" button to view and manage network interfaces.
*   **Comprehensive List**: View all network adapters with details including Name, Description, Status, MAC Address, and Interface Type.
*   **Uninstall Capability**: Easily uninstall stubborn or problematic virtual adapters directly from the toolbar:
    *   **Microsoft WiFi Direct Virtual Adapter**: Often causes issues with hotspots.
    *   **WAN Miniport**: Uninstall all WAN Miniports at once to reset network stack.
    *   **Bluetooth PAN**: Remove Bluetooth Personal Area Network devices.
    *   **Intel WiFi 6E AX210**: Specific target for Intel AX210 driver reset.
*   **Safety Features**: 
    *   Confirmation dialogs before any uninstall action.
    *   Reports success/failure counts.
    *   Automatic list refresh after operations.

### 7. Network Connections Manager
*   **Dedicated Window**: Click the "Connections" button to monitor active TCP/UDP connections.
*   **Active Monitoring**: Real-time list of connections with Process Name, PID, Local/Remote Address, and State.
*   **Who Is**: Automatically resolves external IP addresses to **Organization** and **Country** via `ipinfo.io` (e.g., `Google LLC (US)`).
    *   **Smart Throttling**: Lookups are rate-limited (1 per 3s) to respect API limits.
    *   **Caching**: Results are cached to disk (`ip_cache.json`) for instant future lookups.
*   **Historical Log**: Keeps track of closed connections, perfect for catching short-lived processes or malware beacons.
*   **Mute Function**: Right-click to "Mute" known safe connections (like Localhost or trusted servers) to declutter the view.
*   **Muted Management**: Review muted IPs in a dedicated tab and unmute them if needed.
*   **Persistent Data**: History and Muted settings are saved to disk and restored on next launch.

### 8. AI Assistant (Gemini)
*   **Configuration**: Requires a valid Gemini API key. Create a file named `apikey.txt` in the same folder as the application and paste your API key inside it.
*   **Context-Aware Q&A**: Right-click any row in **ANY** list (Device, Security, Tasks, Adapters, Connections) to "Ask AI about this".
*   **Smart Context**: The app automatically formats the selected item (e.g., process details, event ID) into a prompt for the AI.
*   **Interactive Dialog**: A dedicated chat window lets you ask follow-up questions about the specific system entity.
*   **Intelligent Insights**: Powered by the **Gemini 3 Flash Preview** model for fast and accurate explanations of obscure processes, error codes, and network activity.

## Usage

### Prerequisites
*   Windows 10/11
*   .NET Desktop Runtime (6.0 or higher)

### Installation

1.  **Download** and extract the release zip `DeviceMonitor.zip`.
2.  Right-click `DeviceMonitorCS.exe` and select **Run as Administrator**.
    *   *Note: Administrator rights are required for monitoring Security logs and managing services.*

## Download

You can download the latest version from the [Releases Page](https://github.com/dparksports/DeviceMonitor/releases).

**Direct Download (v1.7.1):**
[DeviceMonitorCS.zip](https://github.com/dparksports/DeviceMonitor/releases/download/v1.7.1/DeviceMonitorCS.zip)
