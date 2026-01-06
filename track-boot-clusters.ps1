# ============================
# CONFIG
# ============================

$start = (Get-Date).AddDays(-90)

# Expected events for a clustered boot
$expectedBootEvents = @(
    @{ Provider = 'Microsoft-Windows-Kernel-General'; Id = 12 },
    @{ Provider = 'Microsoft-Windows-Wininit'; Id = 12 },
    @{ Provider = 'Microsoft-Windows-Security-Auditing'; Id = 4608 }
)


# ============================
# COLLECT BOOT-RELATED EVENTS
# ============================

# Kernel-General 12 (anchor)
$kg12 = Get-WinEvent -FilterHashtable @{
    LogName      = 'System'
    Id           = 12
    ProviderName = 'Microsoft-Windows-Kernel-General'
    StartTime    = $start
} -ErrorAction SilentlyContinue |
Select TimeCreated, Id, ProviderName

if (-not $kg12 -or $kg12.Count -eq 0) {
    Write-Host "No Kernel-General 12 events found in the last 90 days." -ForegroundColor Yellow
    return
}

# Wininit 12
$wininit12 = Get-WinEvent -FilterHashtable @{
    LogName      = 'System'
    Id           = 12
    ProviderName = 'Microsoft-Windows-Wininit'
    StartTime    = $start
} -ErrorAction SilentlyContinue |
Select TimeCreated, Id, ProviderName

# Security 4608
$sec4608 = Get-WinEvent -FilterHashtable @{
    LogName   = 'Security'
    Id        = 4608
    StartTime = $start
} -ErrorAction SilentlyContinue |
Select TimeCreated, Id, ProviderName

# Combine all boot-related events
$rawBoots = @()
$rawBoots += $kg12
$rawBoots += $wininit12
$rawBoots += $sec4608

$rawBoots = $rawBoots | Sort-Object TimeCreated


# ============================
# AUTO-DETECT CLUSTER WINDOW
# ============================

$gaps = @()

foreach ($kg in $kg12) {
    $t = $kg.TimeCreated

    $nextWininit = $wininit12 | Where-Object { $_.TimeCreated -ge $t } | Select-Object -First 1
    $next4608 = $sec4608   | Where-Object { $_.TimeCreated -ge $t } | Select-Object -First 1

    foreach ($evt in @($nextWininit, $next4608)) {
        if ($evt) {
            $gap = ($evt.TimeCreated - $t).TotalSeconds
            if ($gap -gt 0 -and $gap -lt 300) {
                $gaps += $gap
            }
        }
    }
}

$clusterWindow = if ($gaps.Count -gt 0) {
    [math]::Ceiling(($gaps | Measure-Object -Maximum).Maximum + 5)
}
else {
    20
}

Write-Host "Auto-detected clustering window: $clusterWindow seconds"


# ============================
# CLUSTER EVENTS
# ============================

$clusters = @()
$current = @()

foreach ($evt in $rawBoots) {

    if ($current.Count -eq 0) {
        $current += $evt
        continue
    }

    $last = $current[-1]

    if (($evt.TimeCreated - $last.TimeCreated).TotalSeconds -le $clusterWindow) {
        $current += $evt
    }
    else {
        $clusters += , @($current)
        $current = @($evt)
    }
}

if ($current.Count -gt 0) {
    $clusters += , @($current)
}


# ============================
# BUILD BOOT CLUSTERS (ONLY THOSE WITH KERNEL-GENERAL 12)
# ============================

$bootClusters = foreach ($cluster in $clusters) {

    $events = $cluster | Select ProviderName, Id, TimeCreated

    $hasKernel12 = $events | Where-Object {
        $_.ProviderName -eq 'Microsoft-Windows-Kernel-General' -and $_.Id -eq 12
    }

    if ($hasKernel12) {

        # Determine missing expected events
        $missing = foreach ($exp in $expectedBootEvents) {
            $found = $events | Where-Object {
                $_.ProviderName -eq $exp.Provider -and $_.Id -eq $exp.Id
            }
            if (-not $found) {
                "$($exp.Provider) ID $($exp.Id)"
            }
        }

        # Build readable summary
        $eventSummary = ($events |
            Sort-Object TimeCreated |
            ForEach-Object {
                "{0} {1} @ {2}" -f `
                ($_.ProviderName -replace 'Microsoft-Windows-', ''), `
                    $_.Id, `
                    $_.TimeCreated.ToString("HH:mm:ss")
            }) -join "; "

        # Boot time = earliest Kernel-General 12 in cluster
        $bootTime = ($events |
            Where-Object { $_.ProviderName -eq 'Microsoft-Windows-Kernel-General' -and $_.Id -eq 12 } |
            Sort-Object TimeCreated |
            Select-Object -First 1).TimeCreated

        [PSCustomObject]@{
            BootTime      = $bootTime
            Events        = $events
            EventSummary  = $eventSummary
            MissingEvents = $missing -join ", "
        }
    }
}

if (-not $bootClusters -or $bootClusters.Count -eq 0) {
    Write-Host ""
    Write-Host "No boot clusters with Kernel-General 12 found." -ForegroundColor Yellow
    return
}

$bootClusters = $bootClusters | Sort-Object BootTime


# ============================
# SHUTDOWN EVENTS
# ============================

$shutdowns = Get-WinEvent -FilterHashtable @{
    LogName   = 'System'
    Id        = @(4609, 6006)
    StartTime = $start
} -ErrorAction SilentlyContinue |
Select TimeCreated


# ============================
# CALCULATE UPTIME PER BOOT
# ============================

$results = foreach ($i in 0..($bootClusters.Count - 1)) {

    $boot = $bootClusters[$i].BootTime

    $nextBoot = if ($i -lt $bootClusters.Count - 1) {
        $bootClusters[$i + 1].BootTime
    }
    else {
        $null
    }

    $shutdown = $shutdowns |
    Where-Object { $_.TimeCreated -ge $boot } |
    Select-Object -First 1

    if ($shutdown -and $nextBoot) {
        $end = if ($shutdown.TimeCreated -lt $nextBoot) { $shutdown.TimeCreated } else { $nextBoot }
    }
    elseif ($shutdown) { $end = $shutdown.TimeCreated }
    elseif ($nextBoot) { $end = $nextBoot }
    else { $end = $null }

    $uptime = if ($end) { $end - $boot } else { $null }

    [PSCustomObject]@{
        BootTime      = $boot.ToString("yyyy-MM-dd HH:mm:ss")
        ShutdownTime  = if ($end) { $end.ToString("yyyy-MM-dd HH:mm:ss") } else { "" }
        Uptime        = if ($uptime) { $uptime.ToString() } else { "" }
        EventSummary  = $bootClusters[$i].EventSummary
        MissingEvents = $bootClusters[$i].MissingEvents
    }
}

$results | Sort-Object BootTime
