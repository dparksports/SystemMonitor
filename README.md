# ⚔ Auto Command

**Auto Command** is a high-fidelity, professional-grade Windows system monitoring and security dashboard. Built with a focus on visual excellence and real-time accuracy, it provides a "glassmorphism" interface for monitoring critical system health and security status.

[**Download Latest Release (v3.8.1)**](https://github.com/dparksports/SystemMonitor/releases/download/v3.8.1/AutoCommand.exe)

![Auto Command Dashboard](file:///C:/Users/honey/.gemini/antigravity/brain/dd6eed55-9e6b-479d-82bb-0975e0b2df85/uploaded_image_1768036417854.png)

## ✨ Core Features

### 🛡 Real-Time Security Tracking
- **Unified Status**: Instant visibility into Windows Defender and Windows Firewall states.
- **Security Timeline**: Live-updating log of recent security events, firewall rule changes, and device connections.
- **Aurora Visuals**: Dynamic, soft radiant glows ("Aurora" effect) that indicate system health with a premium aesthetic.

### 📈 Performance Visualizer
- **CPU Usage**: Real-time smoothing graph showing processor utilization.
- **Available Memory**: Precise tracking of system RAM, showing exactly how many GB are available for work.
- **Zero-Overlap Design**: High-fidelity chart axis labels and scales designed for perfect readability.

### 💎 Premium Interface
- **Glassmorphism UI**: A modern, translucent material design with integrated title bar controls.
- **Responsive Sidebar**: Collapsible navigation with definitive "Crossed Swords" branding.
- **Dark Mode Optimized**: Deep blue and slate color palette designed for high-end professional setups.

## 🛠 Technical Stack
- **Framework**: .NET 8.0 (WPF) with C# 12
- **Data Sources**: Windows Management Instrumentation (WMI), Windows Event Log API
- **Theming**: Custom Vanilla XAML Styles with `WindowChrome` integration
- **Architecture**: MVVM (Model-View-ViewModel) for clean logic separation

## 🚀 Quick Start
1.  **Clone the Repository**.
2.  **Run as Administrator**: Auto Command requires elevated privileges to query security provider status and event logs.
3.  **Build & Launch**: Use Visual Studio 2022 or the `dotnet` CLI to run the project.

```powershell
dotnet run --project DeviceMonitorCS
```

## 📦 Versioning
Current Stable Release: **v3.8.1** (Definitive Edition)

---
*Developed for power users who demand both data accuracy and visual perfection.*
