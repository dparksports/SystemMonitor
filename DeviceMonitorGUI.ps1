# DeviceMonitorGUI.ps1
# A Native WPF Application for monitoring Device and Security events on Windows.

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName System.ServiceProcess

# XAML Definition
$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Windows System Monitor" Height="600" Width="1000" WindowStartupLocation="CenterScreen">
    <Window.Resources>
        <Style TargetType="DataGrid">
            <Setter Property="AutoGenerateColumns" Value="False"/>
            <Setter Property="CanUserAddRows" Value="False"/>
            <Setter Property="IsReadOnly" Value="True"/>
            <Setter Property="HeadersVisibility" Value="Column"/>
            <Setter Property="GridLinesVisibility" Value="Horizontal"/>
        </Style>
    </Window.Resources>
    <DockPanel>
        <!-- Toolbar -->
        <ToolBarTray DockPanel.Dock="Top">
            <ToolBar>
                <CheckBox Name="VpnToggle" Content="VPN Service" FontSize="14" VerticalAlignment="Center" Margin="5"/>
                <Separator/>
                <Button Name="ClearBtn" Content="Clear Events" Margin="5"/>
            </ToolBar>
        </ToolBarTray>

        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            
            <!-- Left Pane: Device Events -->
            <GroupBox Grid.Column="0" Header="Device Events" Padding="5" Margin="5">
                <DataGrid Name="DeviceGrid">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Time" Binding="{Binding Time}" Width="80"/>
                        <DataGridTextColumn Header="Event" Binding="{Binding EventType}" Width="80"/>
                        <DataGridTextColumn Header="Device Name" Binding="{Binding Name}" Width="150"/>
                        <DataGridTextColumn Header="Type" Binding="{Binding Type}" Width="100"/>
                        <DataGridTextColumn Header="Initiator" Binding="{Binding Initiator}" Width="*"/>
                    </DataGrid.Columns>
                </DataGrid>
            </GroupBox>
            
            <!-- Right Pane: Security Events -->
            <GroupBox Grid.Column="1" Header="Security Events" Padding="5" Margin="5">
                <DataGrid Name="SecurityGrid">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Time" Binding="{Binding Time}" Width="80"/>
                        <DataGridTextColumn Header="EventID" Binding="{Binding Id}" Width="60"/>
                        <DataGridTextColumn Header="Type" Binding="{Binding Type}" Width="110"/>
                        <DataGridTextColumn Header="Activity" Binding="{Binding Activity}" Width="100"/>
                        <DataGridTextColumn Header="Account" Binding="{Binding Account}" Width="*"/>
                    </DataGrid.Columns>
                </DataGrid>
            </GroupBox>
        </Grid>
    </DockPanel>
</Window>
"@

# Helper function to parse XAML
function Read-Xaml {
    param ($Xaml)
    $Reader = [System.Xml.XmlReader]::Create([System.IO.StringReader] $Xaml)
    [System.Windows.Markup.XamlReader]::Load($Reader)
}

# --- Backend Logic Handlers ---

function Get-Initiator {
    try {
        $sys = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction SilentlyContinue
        if ($sys.UserName) { return $sys.UserName }
        return "$env:USERDOMAIN\$env:USERNAME"
    }
    catch { return "Unknown" }
}

# --- VPN Service Logic ---
$vpnServices = @("RasMan", "IKEEXT", "PolicyAgent", "RemoteAccess")

function Update-VpnStatus {
    # Check if ANY of the services are NOT Disabled. If so, we consider VPN "enabled".
    $isEnabled = $false
    foreach ($svcName in $vpnServices) {
        $svc = Get-Service -Name $svcName -ErrorAction SilentlyContinue
        if ($svc -and $svc.StartType -ne 'Disabled') {
            $isEnabled = $true
            break
        }
    }
    return $isEnabled
}

function Set-VpnState {
    param([bool]$Enable)
    
    $startType = if ($Enable) { "Manual" } else { "Disabled" }
    
    foreach ($svcName in $vpnServices) {
        try {
            # 1. Set Startup Type
            # Set-Service can change StartupType
            Set-Service -Name $svcName -StartupType $startType -ErrorAction SilentlyContinue
            
            # 2. Stop if disabling
            if (-not $Enable) {
                Stop-Service -Name $svcName -Force -ErrorAction SilentlyContinue
            }
        }
        catch {
            # Best effort
        }
    }
}

# --- Main Script ---

# Load UI
try {
    $window = Read-Xaml $xaml
}
catch {
    Write-Error "Failed to load XAML: $_"
    exit 1
}

# Find Controls
$deviceGrid = $window.FindName("DeviceGrid")
$securityGrid = $window.FindName("SecurityGrid")
$vpnToggle = $window.FindName("VpnToggle")
$clearBtn = $window.FindName("ClearBtn")

# Data Collections
$deviceData = New-Object System.Collections.ObjectModel.ObservableCollection[Object]
$securityData = New-Object System.Collections.ObjectModel.ObservableCollection[Object]

$deviceGrid.ItemsSource = $deviceData
$securityGrid.ItemsSource = $securityData

# Update Title with User Status
$currentUser = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if ($isAdmin) {
    $window.Title = "Windows System Monitor (Administrator) - User: $currentUser"
    
    # Initialize VPN Toggle State
    $vpnToggle.IsChecked = Update-VpnStatus
    
    # VPN Toggle Event
    $vpnToggle.Add_Click({
            $enabled = $vpnToggle.IsChecked
            Set-VpnState -Enable $enabled
        
            # Log action
            $stateStr = if ($enabled) { "ENABLED (Manual)" } else { "DISABLED (Stopped)" }
            $deviceData.Insert(0, [PSCustomObject]@{
                    Time      = [DateTime]::Now.ToString("HH:mm:ss")
                    EventType = "CONFIG"
                    Name      = "VPN Services"
                    Type      = "System"
                    Initiator = "$stateStr"
                })
        })
}
else {
    $window.Title = "Windows System Monitor (User) - User: $currentUser"
    $vpnToggle.IsEnabled = $false
    $vpnToggle.Content = "VPN (Admin Only)"
    
    $securityData.Add([PSCustomObject]@{
            Time     = "INFO"
            Id       = "-"
            Type     = "-"
            Activity = "Admin Rights Required"
            Account  = "Right-click -> Run as Admin"
        })
}

$clearBtn.Add_Click({
        $deviceData.Clear()
        $securityData.Clear()
    })

# --- Device Monitoring Setup (System.Management) ---
$deviceAction = {
    param($evtSender, $e)
    
    $evtArgs = $e.NewEvent
    $device = $evtArgs.TargetInstance
    $className = $evtArgs.SystemProperties["__Class"].Value
    
    $eventType = "UNKNOWN"
    switch ($className) {
        "__InstanceCreationEvent" { $eventType = "ADDED" }
        "__InstanceDeletionEvent" { $eventType = "REMOVED" }
        "__InstanceModificationEvent" { $eventType = "CHANGED" }
    }

    # Extract Type
    $type = "Device"
    if ($device.PNPClass) { $type = $device.PNPClass }
    elseif ($device.Service) { $type = "Service: $($device.Service)" }

    $name = $device.Name
    if (-not $name) { $name = $device.Description }

    $item = [PSCustomObject]@{
        Time      = [DateTime]::Now.ToString("HH:mm:ss")
        EventType = $eventType
        Name      = $name
        Type      = $type
        Initiator = (Get-Initiator)
    }

    $window.Dispatcher.Invoke([Action] { $deviceData.Insert(0, $item) })
}

# Create Device Watchers - Explicit Property Setting
$qAdd = New-Object System.Management.WqlEventQuery
$qAdd.EventClassName = "__InstanceCreationEvent"
$qAdd.WithinInterval = [TimeSpan]::FromSeconds(1)
$qAdd.Condition = "TargetInstance ISA 'Win32_PnPEntity'"
$wAdd = New-Object System.Management.ManagementEventWatcher $qAdd

$qRem = New-Object System.Management.WqlEventQuery
$qRem.EventClassName = "__InstanceDeletionEvent"
$qRem.WithinInterval = [TimeSpan]::FromSeconds(1)
$qRem.Condition = "TargetInstance ISA 'Win32_PnPEntity'"
$wRem = New-Object System.Management.ManagementEventWatcher $qRem

# Explicit Registration
Register-ObjectEvent -InputObject $wAdd -EventName "EventArrived" -Action $deviceAction | Out-Null
Register-ObjectEvent -InputObject $wRem -EventName "EventArrived" -Action $deviceAction | Out-Null

$wAdd.Start()
$wRem.Start()

# --- Security Monitoring Setup (Polling) ---
$lastRecordId = 0
try {
    $latest = Get-WinEvent -LogName 'Security' -MaxEvents 1 -ErrorAction SilentlyContinue
    if ($latest) { $lastRecordId = $latest.RecordId }
}
catch {}

$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromSeconds(2)
$timer.Add_Tick({
        try {
            if (-not $isAdmin) { return }

            $query = "*[System[(EventID=4624 or EventID=4625) and (EventRecordID > $lastRecordId)]]"
            $events = Get-WinEvent -LogName 'Security' -FilterXml $query -ErrorAction SilentlyContinue | Sort-Object TimeCreated
        
            if ($events) {
                foreach ($evt in $events) {
                    if ($evt.RecordId -gt $lastRecordId) { $lastRecordId = $evt.RecordId }

                    $xml = [xml]$evt.ToXml()
                    $data = $xml.Event.EventData.Data
                
                    $account = ($data | Where-Object { $_.Name -eq "TargetUserName" })."#text"
                    $logonType = ($data | Where-Object { $_.Name -eq "LogonType" })."#text"
                
                    # NO Filtering as requested by user
                    # if ($account -match "SYSTEM|DWM|UMFD") { continue }
                
                    $typeDesc = "LogonType: $logonType"
                    switch ($logonType) {
                        "2" { $typeDesc = "Interactive" }
                        "3" { $typeDesc = "Network" }
                        "4" { $typeDesc = "Batch" }
                        "5" { $typeDesc = "Service" }
                        "7" { $typeDesc = "Unlock" }
                        "10" { $typeDesc = "Remote (RDP)" }
                        "11" { $typeDesc = "Cached" }
                    }

                    $activity = "Logon"
                    if ($evt.Id -eq 4625) { $activity = "Logon Failure" }

                    $item = [PSCustomObject]@{
                        Time     = $evt.TimeCreated.ToString("HH:mm:ss")
                        Id       = $evt.Id
                        Type     = $typeDesc
                        Activity = $activity
                        Account  = $account
                    }
                    $securityData.Insert(0, $item)
                }
            }
        }
        catch { }
    })

$timer.Start()

# Cleanup on Close
$window.Add_Closed({
        $wAdd.Stop(); $wAdd.Dispose()
        $wRem.Stop(); $wRem.Dispose()
        $timer.Stop()
    
        Unregister-Event -SourceIdentifier "*" -ErrorAction SilentlyContinue
    })

# Show Window
$window.ShowDialog() | Out-Null
