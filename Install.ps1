# Install.ps1
# Installs Windows System Monitor and configures UAC bypass via Scheduled Task.

# Self-elevation check
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Requesting Admin privileges for installation..." -ForegroundColor Yellow
    $argList = "-ExecutionPolicy Bypass -File `"$PSCommandPath`""
    Start-Process PowerShell -Verb RunAs -ArgumentList $argList
    exit
}

$ErrorActionPreference = "Stop"
$appName = "DeviceMonitor"
$installDir = "$env:ProgramFiles\$appName"
$scriptName = "DeviceMonitorGUI.ps1"
$taskName = "DeviceMonitorTask"
$sourceScript = Join-Path $PSScriptRoot $scriptName

Write-Host "Installing $appName..." -ForegroundColor Cyan

# 1. Create Directory and Copy Files
try {
    if (-not (Test-Path $installDir)) {
        New-Item -ItemType Directory -Path $installDir -Force | Out-Null
        Write-Host "Created installation directory: $installDir" -ForegroundColor Green
    }
    
    Copy-Item -Path $sourceScript -Destination $installDir -Force
    Write-Host "Copied application files." -ForegroundColor Green
}
catch {
    Write-Error "Failed to copy files: $_"
    exit 1
}

# 2. Create Scheduled Task (UAC Bypass)
try {
    # Delete existing task if any
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

    $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-ExecutionPolicy Bypass -WindowStyle Hidden -File `"$installDir\$scriptName`""
    # $trigger = New-ScheduledTaskTrigger -AtLogOn # Optional, but we mainly want on-demand
    
    # "Run with highest privileges" is the key for UAC bypass
    $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 365)
    
    # We register it without a trigger effectively, just for on-demand use via shortcut
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings -Force | Out-Null
    
    Write-Host "Created Scheduled Task: $taskName (High Privileges)" -ForegroundColor Green
}
catch {
    Write-Error "Failed to create scheduled task: $_"
    exit 1
}

# 3. Create Desktop Shortcut
try {
    $wshShell = New-Object -ComObject WScript.Shell
    $shortcutPath = "$env:USERPROFILE\Desktop\Windows System Monitor.lnk"
    $shortcut = $wshShell.CreateShortcut($shortcutPath)
    
    # The shortcut runs schtasks to trigger the elevated task
    $shortcut.TargetPath = "C:\Windows\System32\schtasks.exe"
    $shortcut.Arguments = "/run /tn `"$taskName`""
    
    # Set icon to PowerShell or a generic monitor icon
    $shortcut.IconLocation = "C:\Windows\System32\dmdskres.dll,0" # Monitor icon
    $shortcut.Description = "Launch Windows System Monitor (Admin)"
    $shortcut.Save()
    
    Write-Host "Created Desktop Shortcut: $shortcutPath" -ForegroundColor Green
}
catch {
    Write-Error "Failed to create shortcut: $_"
    exit 1
}

Write-Host "`nInstallation Complete!" -ForegroundColor Cyan
Write-Host "You can now launch the app from your Desktop without UAC prompts." -ForegroundColor White
Read-Host "Press Enter to exit..."
