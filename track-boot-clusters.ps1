# ============================
# CONFIG
# ============================

# Look back 90 days
$start = (Get-Date).AddDays(-90)

# Expected boot events for YOUR system
$expectedBootEvents = @(
    @{ Provider = 'Microsoft-Windows-Kernel-General'; Id = 12 },
    @{ Provider = 'Microsoft-Windows-Wininit'; Id = 12 },
    @{ Provider = 'Microsoft-Windows-UserModePowerService'; Id = 12 },
    @{ Provider = 'Microsoft-Windows-Security-Auditing'; Id = 4608 },
    @{ Provider = 'EventLog'; Id = 6005 }
)


# ============================
# COLLECT BOOT EVENTS
# ============================

$rawBoots = @()

# System: Kernel-General 12
$rawBoots += Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = 12; ProviderName = 'Microsoft-Windows-Kernel-General'; StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated, Id, ProviderName

# System: Wininit 12
$rawBoots += Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = 12; ProviderName = 'Microsoft-Windows-Wininit'; StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated, Id, ProviderName

# System: UserModePowerService 12
$rawBoots += Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = 12; ProviderName = 'Microsoft-Windows-UserModePowerService'; StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated, Id, ProviderName

# System: EventLog 6005
$rawBoots += Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = 6005; StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated, Id, ProviderName

# Security: 4608
$rawBoots += Get-WinEvent -FilterHashtable @{
    LogName = 'Security'; Id = 4608; StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated, Id, ProviderName

$rawBoots = $rawBoots | Sort-Object TimeCreated


# ============================
# CLUSTER BOOT EVENTS
# ============================

$clusters = @()
$currentCluster = @()

foreach ($evt in $rawBoots) {

    if ($currentCluster.Count -eq 0) {
        $currentCluster += $evt
        continue
    }

    $last = $currentCluster[-1]

    # Same cluster if within 10 seconds
    if (($evt.TimeCreated - $last.TimeCreated).TotalSeconds -le 10) {
        $currentCluster += $evt
    }
    else {
        $clusters += , @($currentCluster)
        $currentCluster = @($evt)
    }
}

if ($currentCluster.Count -gt 0) {
    $clusters += , @($currentCluster)
}

# Convert clusters into boot objects
$boots = foreach ($cluster in $clusters) {

    $clusterEvents = $cluster | Select ProviderName, Id, TimeCreated

    # Determine missing events
    $missing = foreach ($exp in $expectedBootEvents) {
        $found = $clusterEvents | Where-Object {
            $_.ProviderName -eq $exp.Provider -and $_.Id -eq $exp.Id
        }
        if (-not $found) {
            "$($exp.Provider) ID $($exp.Id)"
        }
    }

    [PSCustomObject]@{
        BootTime      = ($cluster | Select-Object -First 1).TimeCreated
        Events        = $clusterEvents
        MissingEvents = if ($missing.Count -gt 0) { $missing -join ", " } else { "" }
    }
}

$boots = $boots | Sort-Object BootTime


# ============================
# SHUTDOWN EVENTS
# ============================

$shutdowns = Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = @(4609, 6006); StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated


# ============================
# LOGON EVENTS
# ============================

$logons = Get-WinEvent -FilterHashtable @{
    LogName = 'Security'; Id = 4624; StartTime = $start
} -ErrorAction SilentlyContinue |
Where-Object {
    $_.Properties[8].Value -in 2, 10 -and
    $_.Properties[5].Value -notmatch '^DWM-' -and
    $_.Properties[5].Value -notmatch '^UMFD-' -and
    $_.Properties[5].Value -ne 'SYSTEM'
} | Select-Object TimeCreated, @{
    Name = 'User'; Expression = { $_.Properties[5].Value }
}, @{
    Name = 'LogonID'; Expression = { $_.Properties[7].Value }
}, @{
    Name = 'LogonType'; Expression = { $_.Properties[8].Value }
}, @{
    Name = 'SourceIP'; Expression = { $_.Properties[18].Value }
}

# Deduplicate unlocks
$logons = $logons |
Group-Object LogonID |
ForEach-Object { $_.Group | Sort-Object TimeCreated | Select-Object -First 1 }


# ============================
# LOGOFF EVENTS
# ============================

$logoffs = Get-WinEvent -FilterHashtable @{
    LogName = 'Security'; Id = 4634; StartTime = $start
} -ErrorAction SilentlyContinue |
Select-Object TimeCreated, @{
    Name = 'LogonID'; Expression = { $_.Properties[0].Value }
}


# ============================
# BUILD BOOT CYCLES
# ============================

$rows = foreach ($i in 0..($boots.Count - 1)) {

    $boot = $boots[$i].BootTime

    # Next boot
    $nextBoot = if ($i -lt $boots.Count - 1) { $boots[$i + 1].BootTime } else { $null }

    # Next shutdown
    $shutdown = $shutdowns |
    Where-Object { $_.TimeCreated -ge $boot } |
    Select-Object -First 1

    # Determine cycle end
    if ($shutdown -and $nextBoot) {
        $cycleEnd = if ($shutdown.TimeCreated -lt $nextBoot) { $shutdown.TimeCreated } else { $nextBoot }
    }
    elseif ($shutdown) { $cycleEnd = $shutdown.TimeCreated }
    elseif ($nextBoot) { $cycleEnd = $nextBoot }
    else { $cycleEnd = $null }

    # Logons in this cycle
    $sessionLogons = $logons |
    Where-Object {
        $_.TimeCreated -ge $boot -and
        ($cycleEnd -eq $null -or $_.TimeCreated -le $cycleEnd)
    }

    # Flatten sessions
    $sessionText = foreach ($logon in $sessionLogons) {

        $logoff = $logoffs |
        Where-Object { $_.LogonID -eq $logon.LogonID } |
        Select-Object -First 1

        $end = if ($logoff) { $logoff.TimeCreated }
        elseif ($cycleEnd) { $cycleEnd }
        else { $null }

        "$($logon.User) | $($logon.TimeCreated) | $($logoff.TimeCreated) | $end | $($end - $logon.TimeCreated) | Type $($logon.LogonType) | $($logon.SourceIP)"
    }

    [PSCustomObject]@{
        BootTime      = $boot.ToString("yyyy-MM-dd HH:mm:ss")
        MissingEvents = $boots[$i].MissingEvents
        CycleEnd      = if ($cycleEnd) { $cycleEnd.ToString("yyyy-MM-dd HH:mm:ss") } else { "" }
        Sessions      = $sessionText -join "`n"
    }
}

$rows | Sort-Object BootTime
