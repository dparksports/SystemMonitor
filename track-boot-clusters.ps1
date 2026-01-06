# ============================
# CONFIG
# ============================

$start = (Get-Date).AddDays(-90)

# Expected boot events for your system
$expectedBootEvents = @(
    @{ Provider = 'Microsoft-Windows-Kernel-General'; Id = 12 },
    @{ Provider = 'Microsoft-Windows-Wininit'; Id = 12 },
    @{ Provider = 'Microsoft-Windows-Security-Auditing'; Id = 4608 },
    @{ Provider = 'EventLog'; Id = 6005 }
)


# ============================
# COLLECT BOOT-RELATED EVENTS
# ============================

# Kernel-General 12 (anchor)
$kernelBoots = Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = 12; ProviderName = 'Microsoft-Windows-Kernel-General'; StartTime = $start
} -ErrorAction SilentlyContinue |
Select TimeCreated, Id, ProviderName |
Sort-Object TimeCreated

if (-not $kernelBoots) {
    Write-Host "No Kernel-General 12 boot events found."
    return
}

# Wininit 12
$wininit = Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = 12; ProviderName = 'Microsoft-Windows-Wininit'; StartTime = $start
} -ErrorAction SilentlyContinue |
Select TimeCreated, Id, ProviderName

# EventLog 6005
$event6005 = Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = 6005; StartTime = $start
} -ErrorAction SilentlyContinue |
Select TimeCreated, Id, ProviderName

# Security 4608
$sec4608 = Get-WinEvent -FilterHashtable @{
    LogName = 'Security'; Id = 4608; StartTime = $start
} -ErrorAction SilentlyContinue |
Select TimeCreated, Id, ProviderName


# ============================
# AUTO-DETECT CLUSTERING WINDOW
# ============================

$gaps = @()

foreach ($kg in $kernelBoots) {
    $t = $kg.TimeCreated

    $nextWininit = $wininit  | Where-Object { $_.TimeCreated -ge $t } | Select-Object -First 1
    $next6005 = $event6005 | Where-Object { $_.TimeCreated -ge $t } | Select-Object -First 1
    $next4608 = $sec4608  | Where-Object { $_.TimeCreated -ge $t } | Select-Object -First 1

    foreach ($evt in @($nextWininit, $next6005, $next4608)) {
        if ($evt) {
            $gap = ($evt.TimeCreated - $t).TotalSeconds
            if ($gap -gt 0 -and $gap -lt 300) {
                # ignore huge gaps
                $gaps += $gap
            }
        }
    }
}

if ($gaps.Count -gt 0) {
    $clusteringWindow = [math]::Ceiling(($gaps | Measure-Object -Maximum).Maximum + 5)
}
else {
    $clusteringWindow = 30
}

Write-Host "Auto-detected clustering window: $clusteringWindow seconds"


# ============================
# BUILD RAW BOOT EVENT LIST
# ============================

$rawBoots = @()
$rawBoots += $kernelBoots
$rawBoots += $wininit
$rawBoots += $event6005
$rawBoots += $sec4608

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

    if (($evt.TimeCreated - $last.TimeCreated).TotalSeconds -le $clusteringWindow) {
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


# ============================
# BUILD CLUSTER OBJECTS
# ============================

$boots = foreach ($cluster in $clusters) {

    $clusterEvents = $cluster | Select ProviderName, Id, TimeCreated

    $missing = foreach ($exp in $expectedBootEvents) {
        $found = $clusterEvents | Where-Object {
            $_.ProviderName -eq $exp.Provider -and $_.Id -eq $exp.Id
        }
        if (-not $found) {
            "$($exp.Provider) ID $($exp.Id)"
        }
    }

    $eventSummary = ($clusterEvents |
        Sort-Object TimeCreated |
        ForEach-Object {
            "{0} {1} @ {2}" -f `
            ($_.ProviderName -replace 'Microsoft-Windows-', ''), `
                $_.Id, `
                $_.TimeCreated.ToString("HH:mm:ss")
        }) -join "; "

    [PSCustomObject]@{
        BootTime      = ($cluster | Select-Object -First 1).TimeCreated.ToString("yyyy-MM-dd HH:mm:ss")
        EventSummary  = $eventSummary
        MissingEvents = $missing -join ", "
    }
}

$boots | Sort-Object BootTime
