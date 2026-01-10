# Auto Command

**Advanced System Management & Security Hardening**

Auto Command is a powerful, unified tool designed for Windows administrators and power users who need granular control over their system's security posture and underlying hardware. Built with a modern, darkened aesthetic and high-performance native integrations, it streamlines complex administrative tasks into a single, cohesive dashboard.

---

## 🚀 Key Features

### 🛡️ Unified Command Panel
The heart of Auto Command, providing one-click management for critical system interfaces often exploited as "Security Holes."
- **Network Hole Management**: Instantly toggle and natively uninstall WAN Miniports, WiFi Direct Virtual Adapters, and Kernel Debug Network Adapters (KDNET).
- **Privacy Toggles**: Global control over background VPN services, wireless entry points, and Windows flighting/telemetry tasks.
- **Real-time Monitoring**: Integrated status checks for Windows Defender Tamper Protection and boot configuration flags.

### 🔍 Advanced Diagnostics
- **Firmware Explorer**: Deep dive into ACPI tables and UEFI variables with detailed byte-level views.
- **Security Timeline**: Track historical system events to identify suspicious patterns or configuration changes.
- **Device Management**: A high-performance replacement for Device Manager with advanced property enumeration and native uninstallation capabilities.

### ⚙️ System Tools
- **Scheduled Tasks**: Browse and audit Windows tasks with an intuitive interface.
- **Network Connections**: High-visibility monitoring of active network sockets and traffic.
- **True Shutdown**: Bypass Windows "Fast Startup" to perform a complete hardware power-off, ensuring a fresh state on the next boot.
- **Firewall Controls**: Streamlined management of profiles and rule sets.

---

## 🛠️ Technology Stack

- **Core Framework**: .NET 8.0 (WPF)
- **Languages**: C#, XAML, PowerShell
- **System APIs**: 
    - **WMI** (`System.Management`) for advanced hardware queries.
    - **SetupDi API** (P/Invoke) for native device management and driver uninstallation.
    - **BCDedit** for boot configuration management.
- **UI Design**: Modern, dark-mode focused UI with accent-driven typography and micro-animations.

---

## 📥 Getting Started

### ⚡ Quick Start
The easiest way to get started is to download the latest production bundle:
- **[Download Auto Command v3.4 (.zip)](https://github.com/dparksports/SystemMonitor/releases/download/v3.4/AutoCommand_v3.4.zip)**
- Or browse all **[Releases](https://github.com/dparksports/SystemMonitor/releases)**

### Build from Source
If you prefer to build it yourself:
1. Clone the repository.
2. Open the solution in Visual Studio or your preferred C# IDE.
3. Build the project using the **Release** configuration.
4. Run `AutoCommand.exe` from the output directory.

### Prerequisites
- .NET 8.0 Runtime (for running) or SDK (for building).
- Windows 10/11 with Administrative privileges.

### Optional: Firebase Telemetry
The application includes optional telemetry for tracking usage metrics (app starts, UI interactions). To enable this:
1. Create a `firebase_config.json` file in the `DeviceMonitorCS` directory.
2. Use `firebase_config.json.template` as a guide and fill in your `apiKey` and `measurementId`.
3. Rebuild the project.

---

## ⚖️ License

Auto Command is released under the **Apache License, Version 2.0**.

Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. You may obtain a copy of the License at: [http://www.apache.org/licenses/LICENSE-2.0](http://www.apache.org/licenses/LICENSE-2.0)

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.

---
Created with ❤️ in California.
